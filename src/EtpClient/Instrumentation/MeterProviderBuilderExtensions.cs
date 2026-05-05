using OpenTelemetry.Metrics;

namespace EtpClient.Instrumentation;

/// <summary>
/// Extension methods for <see cref="MeterProviderBuilder"/> to register ETP instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Registers the ETP meter (<c>"EtpClient"</c>) into the application's meter provider.
    /// Follows the same pattern as <c>AddAspNetCoreInstrumentation()</c>.
    /// Calling this method more than once on the same builder is safe and idempotent.
    /// </summary>
    /// <param name="builder">The meter provider builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static MeterProviderBuilder AddEtpInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(EtpInstrumentation.EtpMeter.Name);
    }
}
