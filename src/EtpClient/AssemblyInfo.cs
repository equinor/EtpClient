using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EtpClient.UnitTests")]
[assembly: InternalsVisibleTo("EtpClient.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]  // Required by NSubstitute for mocking internals
