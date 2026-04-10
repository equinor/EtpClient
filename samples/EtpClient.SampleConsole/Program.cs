using EtpClient.SampleConsole;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Explicitly load user secrets so the sample works regardless of DOTNET_ENVIRONMENT
builder.Configuration.AddUserSecrets<Program>();

// Bind sample options from the "Etp" configuration section
var sampleOptions = new SampleConsoleOptions();
builder.Configuration.GetSection("Etp").Bind(sampleOptions);

builder.Services.AddSingleton(sampleOptions);
builder.Services.AddSingleton<SampleOutputWriter>();
builder.Services.AddTransient<SampleConsoleRunner>();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

using var host = builder.Build();

var runner = host.Services.GetRequiredService<SampleConsoleRunner>();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var outcome = await runner.RunAsync(cts.Token);
return outcome.ToExitCode();
