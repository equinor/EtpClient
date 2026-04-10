namespace EtpClient.Models;

/// <summary>A protocol version as declared in the ETP SupportedProtocol Avro record.</summary>
/// <param name="Major">Major version number.</param>
/// <param name="Minor">Minor version number.</param>
/// <param name="Revision">Revision number.</param>
/// <param name="Patch">Patch number.</param>
public sealed record ProtocolVersion(int Major, int Minor, int Revision, int Patch)
{
    /// <summary>ETP v1.1 protocol version.</summary>
    public static readonly ProtocolVersion Etp11 = new(1, 1, 0, 0);

    /// <inheritdoc/>
    public override string ToString() => $"{Major}.{Minor}.{Revision}.{Patch}";
}

/// <summary>A protocol capability advertised during ETP session negotiation.</summary>
/// <param name="Protocol">ETP protocol number (e.g. 0 = Core, 1 = ChannelStreaming).</param>
/// <param name="Version">Protocol version.</param>
/// <param name="Role">Role declared by this party (e.g. "consumer", "producer", "server").</param>
public sealed record SupportedProtocol(int Protocol, ProtocolVersion Version, string Role);

/// <summary>Session details returned in the ETP OpenSession message.</summary>
public sealed class NegotiatedSessionInfo
{
    /// <summary>Server-assigned instance identifier (UUID bytes).</summary>
    public required Guid ServerInstanceId { get; init; }

    /// <summary>Protocols accepted by the server.</summary>
    public required IReadOnlyList<SupportedProtocol> SupportedProtocols { get; init; }

    /// <summary>Compression format selected by the server (empty string means none).</summary>
    public required string SupportedCompression { get; init; }

    /// <summary>Data formats supported by the server (e.g. "xml").</summary>
    public required IReadOnlyList<string> SupportedFormats { get; init; }

    /// <summary>Server application name reported in OpenSession.</summary>
    public required string ServerApplicationName { get; init; }

    /// <summary>Server application version reported in OpenSession.</summary>
    public required string ServerApplicationVersion { get; init; }

    /// <summary>True when the server negotiated Protocol 3 (Discovery) for this session.</summary>
    public bool SupportsDiscovery => SupportedProtocols.Any(protocol => protocol.Protocol == 3);
}
