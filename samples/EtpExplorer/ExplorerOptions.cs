using EtpClient.Models;

namespace EtpExplorer;

/// <summary>
/// Configuration options for the ETP Explorer application.
/// Binds from the <c>Etp</c> configuration section (user secrets + appsettings).
/// </summary>
public sealed class ExplorerOptions
{
    /// <summary>Absolute <c>ws</c> or <c>wss</c> URI for the ETP WebSocket endpoint.</summary>
    public string? EndpointUri { get; set; }

    /// <summary>Basic authentication username.</summary>
    public string? Username { get; set; }

    /// <summary>Basic authentication password.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// ETP message encoding to use for the connection.
    /// Accepted values: <c>Binary</c> (default) or <c>Json</c>.
    /// </summary>
    public EtpMessageEncoding MessageEncoding { get; set; } = EtpMessageEncoding.Binary;

    /// <summary>
    /// Maximum number of seconds to wait for Discovery/Describe protocol request/response.
    /// Must be greater than 0.
    /// </summary>
    public int ProtocolRequestTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Validates that all required fields are present and well-formed.
    /// Returns <see langword="null"/> on success, or a secret-safe setup message describing what is missing or invalid.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(EndpointUri))
            return
                "Etp:EndpointUri is required.\n" +
                "Set it via: dotnet user-secrets set \"Etp:EndpointUri\" \"wss://your-server/etp\"";

        if (!Uri.TryCreate(EndpointUri, UriKind.Absolute, out var uri))
            return "Etp:EndpointUri is not a valid absolute URI.";

        if (!uri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
            return "Etp:EndpointUri scheme must be 'ws' or 'wss'.";

        if (string.IsNullOrWhiteSpace(Username))
            return
                "Etp:Username is required.\n" +
                "Set it via: dotnet user-secrets set \"Etp:Username\" \"your-username\"";

        if (string.IsNullOrWhiteSpace(Password))
            return
                "Etp:Password is required.\n" +
                "Set it via: dotnet user-secrets set \"Etp:Password\" \"your-password\"";

        if (ProtocolRequestTimeoutSeconds <= 0)
            return "Etp:ProtocolRequestTimeoutSeconds must be greater than 0.";

        return null;
    }
}
