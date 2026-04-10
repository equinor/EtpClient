namespace EtpClient.IntegrationTests;

/// <summary>
/// A <see cref="Xunit.FactAttribute"/> variant that automatically skips the test
/// when live-server environment variables are not set.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class LiveFactAttribute : Xunit.FactAttribute
{
    public LiveFactAttribute()
    {
        var settings = LiveServerSettings.FromUserSecrets();
            settings = settings.IsConfigured ? settings : LiveServerSettings.FromEnvironment();
            
        if (!settings.IsConfigured)
            Skip = $"Live server not configured – {settings.MissingReason}";
    }
}
