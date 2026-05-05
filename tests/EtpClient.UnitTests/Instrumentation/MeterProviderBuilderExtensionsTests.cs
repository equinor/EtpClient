using EtpClient.Instrumentation;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace EtpClient.UnitTests.Instrumentation;

[Collection("EtpInstrumentation")]
public sealed class MeterProviderBuilderExtensionsTests
{
    [Fact]
    public void AddEtpInstrumentation_RegistersEtpClientMeter()
    {
        // Arrange & Act — build a MeterProvider with AddEtpInstrumentation, then record a
        // measurement and verify it is exported (collected count > 0).
        var exported = new List<Metric>();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddEtpInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        EtpInstrumentation.MessagesSent.Add(1);
        provider.ForceFlush();

        Assert.Contains(exported, m => m.Name == "etp.client.messages.sent");
    }

    [Fact]
    public void AddEtpInstrumentation_CalledTwice_DoesNotThrow()
    {
        // Idempotency: calling AddEtpInstrumentation twice must not throw.
        var exception = Record.Exception(() =>
        {
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddEtpInstrumentation()
                .AddEtpInstrumentation()
                .Build();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddEtpInstrumentation_ReturnsBuilderForChaining()
    {
        MeterProviderBuilder? returned = null;
        var builder = Sdk.CreateMeterProviderBuilder();

        returned = builder.AddEtpInstrumentation();

        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithoutAddEtpInstrumentation_NoMeasurementsExported()
    {
        var exported = new List<Metric>();
        using var provider = Sdk.CreateMeterProviderBuilder()
            // intentionally NOT calling AddEtpInstrumentation
            .AddInMemoryExporter(exported)
            .Build();

        EtpInstrumentation.MessagesSent.Add(1);
        provider.ForceFlush();

        Assert.DoesNotContain(exported, m => m.Name == "etp.client.messages.sent");
    }
}
