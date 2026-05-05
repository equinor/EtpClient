using OpenTelemetry.Trace;

namespace EtpClient.Instrumentation;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> to register ETP instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Registers the ETP activity source (<c>"EtpClient"</c>) into the application's tracer provider.
    /// Follows the same pattern as <c>AddAspNetCoreInstrumentation()</c>.
    /// Calling this method more than once on the same builder is safe and idempotent.
    /// </summary>
    /// <param name="builder">The tracer provider builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static TracerProviderBuilder AddEtpInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(EtpInstrumentation.Source.Name);
    }
}
