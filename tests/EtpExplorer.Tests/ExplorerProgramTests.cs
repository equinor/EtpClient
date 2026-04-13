using EtpClient.Models;
using EtpExplorer.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpExplorer.Tests;

/// <summary>
/// Tests for the <see cref="ExplorerApp"/> startup validation and shutdown flow.
/// </summary>
public sealed class ExplorerProgramTests
{
    private static ExplorerApp BuildApp(
        ExplorerOptions options,
        FakeExplorerClient? client = null,
        FakeExplorerUi? ui = null,
        SelectionSetService? selectionSet = null)
    {
        var fakeClient = client ?? new FakeExplorerClient();
        var fakeUi = ui ?? new FakeExplorerUi();

        return new ExplorerApp(
            fakeClient,
            fakeUi,
            options,
            new ExplorerBrowseService(NullLogger<ExplorerBrowseService>.Instance),
            new ExplorerEndpointResolver(NullLogger<ExplorerEndpointResolver>.Instance),
            selectionSet ?? new SelectionSetService(),
            new ExplorerStreamingService(NullLogger<ExplorerStreamingService>.Instance),
            NullLogger<ExplorerApp>.Instance);
    }

    // ── Configuration validation ───────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithMissingEndpointUri_ReturnsExitCode1AndShowsConfigError()
    {
        var options = new ExplorerOptions { Username = "u", Password = "p" };
        var ui = new FakeExplorerUi();
        var app = BuildApp(options, ui: ui);

        var result = await app.RunAsync();

        Assert.Equal(1, result);
        Assert.Single(ui.ConfigErrorMessages);
        Assert.Contains("EndpointUri", ui.ConfigErrorMessages[0]);
    }

    [Fact]
    public async Task RunAsync_WithMissingUsername_ReturnsExitCode1AndShowsConfigError()
    {
        var options = new ExplorerOptions { EndpointUri = "wss://x/etp", Password = "p" };
        var ui = new FakeExplorerUi();
        var app = BuildApp(options, ui: ui);

        var result = await app.RunAsync();

        Assert.Equal(1, result);
        Assert.Single(ui.ConfigErrorMessages);
    }

    [Fact]
    public async Task RunAsync_ConfigError_MessageNeverContainsCredentials()
    {
        const string user = "my-user";
        const string pass = "my-pass";
        var options = new ExplorerOptions { Username = user, Password = pass }; // missing URI

        var ui = new FakeExplorerUi();
        var app = BuildApp(options, ui: ui);
        await app.RunAsync();

        var configMsg = string.Join(" ", ui.ConfigErrorMessages);
        Assert.DoesNotContain(user, configMsg);
        Assert.DoesNotContain(pass, configMsg);
    }

    // ── Connection failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenConnectThrows_ReturnsExitCode1AndShowsError()
    {
        var options = ValidOptions();
        var client = new FakeExplorerClient
        {
            ConnectException = new InvalidOperationException("connection refused")
        };
        var ui = new FakeExplorerUi();
        var app = BuildApp(options, client, ui);

        var result = await app.RunAsync();

        Assert.Equal(1, result);
        Assert.Single(ui.ErrorMessages);
        Assert.Contains("Connection failed", ui.ErrorMessages[0]);
        // Must not echo URI or credentials
        Assert.DoesNotContain("secret-pass", ui.ErrorMessages[0]);
    }

    // ── Empty root nodes ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenNeitherRootNodesDiscovered_ReturnsExitCode1AndShowsError()
    {
        var options = ValidOptions();
        var client = new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildEmptyDiscoveryResult("eml://"),
        };
        var ui = new FakeExplorerUi();
        var app = BuildApp(options, client, ui);

        var result = await app.RunAsync();

        Assert.Equal(1, result);
        Assert.NotEmpty(ui.ErrorMessages);
    }

    // ── Clean exit from main menu ──────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenUserSelectsExit_ReturnsExitCode0()
    {
        var options = ValidOptions();
        var client = BuildClientWithRootNodes("witsml14", "witsml20");
        var ui = new FakeExplorerUi();

        // User selects the first root node, then exits
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(options, client, ui);
        var result = await app.RunAsync();

        Assert.Equal(0, result);
        Assert.Equal(1, client.CloseCallCount);
    }

    // ── Session close on shutdown ─────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_OnExit_ClosesClientSession()
    {
        var options = ValidOptions();
        var client = BuildClientWithRootNodes("witsml14");
        var ui = new FakeExplorerUi();
        ui.EnqueueRootNode(new RootNodeOption { Name = "witsml14", Uri = "eml:///witsml14" });
        ui.EnqueueMainMenu(MainMenuAction.Exit);

        var app = BuildApp(options, client, ui);
        await app.RunAsync();

        Assert.Equal(1, client.CloseCallCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExplorerOptions ValidOptions() => new()
    {
        EndpointUri = "wss://fake-server/etp",
        Username = "user",
        Password = "secret-pass",
        ProtocolRequestTimeoutSeconds = 5,
    };

    private static FakeExplorerClient BuildClientWithRootNodes(params string[] names)
    {
        var resources = names
            .Select(n => FakeExplorerClient.BuildResource($"eml:///{n}", n))
            .ToList();

        return new FakeExplorerClient
        {
            DiscoverResult = FakeExplorerClient.BuildDiscoveryResult("eml://", resources),
        };
    }
}
