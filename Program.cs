using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Enrichers.WithCaller;
using Serilog.Events;
using djiconnect.Services;
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging();
builder.Services.AddSingleton<ConnectService>();
builder.Services.AddHostedService(remoteClient => remoteClient.GetRequiredService<ConnectService>());
builder.Configuration.AddJsonFile($"appsettings.json", optional: false, reloadOnChange: true);
LoggingLevelSwitch levelSwitch = new(); // Create the LoggingLevelSwitch
var minimumLevelString = builder.Configuration["Serilog:MinimumLevel:Default"]; // Get the minimum level as string
if (Enum.TryParse(minimumLevelString, out LogEventLevel minimumLevel))
{
    levelSwitch.MinimumLevel = minimumLevel; // Set the level from the configuration
}
else
{
    levelSwitch.MinimumLevel = LogEventLevel.Information; // Default to Information if parsing fails
}
builder.Services.AddSingleton(levelSwitch);
Log.Logger = new Serilog.LoggerConfiguration().ReadFrom.Configuration(builder.Configuration)
.Enrich.WithCaller()
.MinimumLevel.ControlledBy(levelSwitch) // Use the LoggingLevelSwitch
.CreateLogger();
builder.Logging.ClearProviders().AddSerilog(Log.Logger);
IHost app = builder.Build();
await app.RunAsync();