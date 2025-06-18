using Serilog;

var logToConsole = Environment.GetEnvironmentVariable("LOG_CONSOLE_ENABLED")?.ToLower() == "true";

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        "/logs/smartlease-.log",            // rotation journalière
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,          // garder 7 jours
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    );

if (logToConsole)
{
    loggerConfig = loggerConfig.WriteTo.Console();
}

Log.Logger = loggerConfig.CreateLogger();

try
{
    Log.Information("Starting uploader");
    await new SmartleaseUploader.Uploader().StartAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Uploader terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
