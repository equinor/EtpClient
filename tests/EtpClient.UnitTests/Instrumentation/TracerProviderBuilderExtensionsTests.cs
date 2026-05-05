using System.Diagnostics;
using EtpClient.Instrumentation;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace EtpClient.UnitTests.Instrumentation;

[Collection("EtpInstrumentation")]
public sealed class TracerProviderBuilderExtensionsTests
{
    [Fact]
    public void AddEtpInstrumentation_RegistersEtpClientSource()
    {
        // Arrange & Act — build a TracerProvider with AddEtpInstrumentation, then start an
        // activity from the EtpClient source and verify it is sampled (non-null).
        Activity? captured = null;
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(new List<Activity>())
            .Build();

        using var activity = EtpInstrumentation.Source.StartActivity("test.op");
        captured = activity;

        Assert.NotNull(captured);
    }

    [Fact]
    public void AddEtpInstrumentation_CalledTwice_DoesNotThrow()
    {
        // Idempotency: calling AddEtpInstrumentation twice must not throw.
        var exception = Record.Exception(() =>
        {
            using var provider = Sdk.CreateTracerProviderBuilder()
                .AddEtpInstrumentation()
                .AddEtpInstrumentation()
                .Build();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddEtpInstrumentation_ReturnsBuilderForChaining()
    {
        // The method must return the same builder instance so that fluent chaining compiles.
        TracerProviderBuilder? returned = null;
        var builder = Sdk.CreateTracerProviderBuilder();

        returned = builder.AddEtpInstrumentation();

        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithoutAddEtpInstrumentation_EtpSourceNotListened()
    {
        // Without the registration, ActivitySource.StartActivity returns null
        // because no listener is attached to "EtpClient".
        using var provider = Sdk.CreateTracerProviderBuilder()
            // intentionally NOT calling AddEtpInstrumentation
            .Build();

        using var activity = EtpInstrumentation.Source.StartActivity("test.unlistened");

        Assert.Null(activity);
    }
}
