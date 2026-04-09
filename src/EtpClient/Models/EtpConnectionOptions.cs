using System.Text.RegularExpressions;

namespace EtpClient.Models;

/// <summary>
/// Immutable configuration required to open an authenticated ETP connection.
/// Credentials are held as <see langword="string"/> values; callers are responsible for
/// protecting them in memory (e.g. via SecureString or environment injection) before
/// constructing this object. The type itself never logs or serializes credentials.
/// </summary>
public sealed class EtpConnectionOptions
{
    private static readonly HashSet<string> AllowedSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "ws", "wss" };

    /// <summary>
    /// WebSocket endpoint URI. Must be absolute with scheme <c>ws</c> or <c>wss</c>.
    /// </summary>
    public Uri EndpointUri { get; }

    /// <summary>Basic authentication user name. Not included in any log or error output.</summary>
    public string Username { get; }

    /// <summary>Basic authentication password. Not included in any log or error output.</summary>
    public string Password { get; }

    /// <summary>Client instance identifier sent in the ETP RequestSession message.</summary>
    public Guid ClientInstanceId { get; }

    /// <summary>
    /// Protocol capabilities to advertise in RequestSession.
    /// Defaults to an empty list (no specific protocol capabilities declared).
    /// </summary>
    public IReadOnlyList<SupportedProtocol> RequestedProtocols { get; }

    /// <summary>WebSocket keep-alive interval. Defaults to 30 seconds.</summary>
    public TimeSpan KeepAliveInterval { get; }

    /// <summary>Maximum time allowed for the connection and session handshake.</summary>
    public TimeSpan ConnectionTimeout { get; }

    /// <summary>
    /// Constructs and validates a new <see cref="EtpConnectionOptions"/> instance.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when any required field is invalid.</exception>
    public EtpConnectionOptions(
        Uri endpointUri,
        string username,
        string password,
        Guid? clientInstanceId = null,
        IReadOnlyList<SupportedProtocol>? requestedProtocols = null,
        TimeSpan? keepAliveInterval = null,
        TimeSpan? connectionTimeout = null)
    {
        if (endpointUri is null)
            throw new ArgumentNullException(nameof(endpointUri));
        if (!endpointUri.IsAbsoluteUri)
            throw new ArgumentException("EndpointUri must be an absolute URI.", nameof(endpointUri));
        if (!AllowedSchemes.Contains(endpointUri.Scheme))
            throw new ArgumentException(
                $"EndpointUri scheme must be 'ws' or 'wss', got '{endpointUri.Scheme}'.",
                nameof(endpointUri));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username must not be blank.", nameof(username));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password must not be blank.", nameof(password));

        EndpointUri = endpointUri;
        Username = username;
        Password = password;
        ClientInstanceId = clientInstanceId ?? Guid.NewGuid();
        RequestedProtocols = requestedProtocols ?? Array.Empty<SupportedProtocol>();
        KeepAliveInterval = keepAliveInterval ?? TimeSpan.FromSeconds(30);
        ConnectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(30);
    }
}
