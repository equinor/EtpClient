using EtpExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Explicitly load user secrets so the explorer works regardless of DOTNET_ENVIRONMENT
builder.Configuration.AddUserSecrets<Program>();

// Bind explorer options from the "Etp" configuration section
var explorerOptions = new ExplorerOptions();
builder.Configuration.GetSection("Etp").Bind(explorerOptions);

builder.Services.AddSingleton(explorerOptions);
builder.Services.AddTransient<ExplorerBrowseService>();
builder.Services.AddTransient<ExplorerEndpointResolver>();
builder.Services.AddSingleton<SelectionSetService>();
builder.Services.AddTransient<ExplorerStreamingService>();
builder.Services.AddSingleton<IExplorerUi, SpectreExplorerUi>();
builder.Services.AddTransient<IExplorerClient>(_ =>
    new EtpClientAdapter(new EtpClient.EtpClient()));
builder.Services.AddTransient<ExplorerApp>();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

using var host = builder.Build();

var app = host.Services.GetRequiredService<ExplorerApp>();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

return await app.RunAsync(cts.Token);
