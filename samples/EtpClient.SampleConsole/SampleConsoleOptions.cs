using EtpClient.Models;

namespace EtpClient.SampleConsole;

/// <summary>
/// Configuration options for the ETP sample console application.
/// Binds from the <c>Etp</c> configuration section (user secrets + appsettings).
/// </summary>
public sealed class SampleConsoleOptions
{
    /// <summary>Absolute <c>ws</c> or <c>wss</c> URI for the ETP WebSocket endpoint.</summary>
    public string? EndpointUri { get; set; }

    /// <summary>Basic authentication username.</summary>
    public string? Username { get; set; }

    /// <summary>Basic authentication password.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the success summary includes negotiated session details.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool ShowSessionDetails { get; set; }

    /// <summary>
    /// ETP message encoding to use for the connection.
    /// Accepted values: <c>Binary</c> (default) or <c>Json</c>.
    /// </summary>
    public EtpMessageEncoding MessageEncoding { get; set; } = EtpMessageEncoding.Binary;

    /// <summary>
    /// Validates that all required fields are present and well-formed.
    /// Returns <see langword="null"/> on success, or a secret-safe message describing what is missing or invalid.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(EndpointUri))
            return "Etp:EndpointUri is required. Set it via: dotnet user-secrets set \"Etp:EndpointUri\" \"wss://your-server/etp\"";

        if (!Uri.TryCreate(EndpointUri, UriKind.Absolute, out var uri))
            return $"Etp:EndpointUri is not a valid absolute URI.";

        if (!uri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
            return $"Etp:EndpointUri scheme must be 'ws' or 'wss'.";

        if (string.IsNullOrWhiteSpace(Username))
            return "Etp:Username is required. Set it via: dotnet user-secrets set \"Etp:Username\" \"your-username\"";

        if (string.IsNullOrWhiteSpace(Password))
            return "Etp:Password is required. Set it via: dotnet user-secrets set \"Etp:Password\" \"your-password\"";

        return null;
    }

    /// <summary>
    /// Converts the validated options into <see cref="EtpConnectionOptions"/>.
    /// Call <see cref="Validate"/> first.
    /// </summary>
    public EtpConnectionOptions ToConnectionOptions() =>
        new(new Uri(EndpointUri!), Username!, Password!, messageEncoding: MessageEncoding);
}
