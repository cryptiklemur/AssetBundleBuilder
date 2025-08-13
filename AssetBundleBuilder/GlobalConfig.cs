using Serilog;
using Serilog.Events;

namespace CryptikLemur.AssetBundleBuilder;

public enum VerbosityLevel
{
    Quiet = 0,    // Only errors
    Normal = 1,   // Errors and warnings
    Verbose = 2,  // Errors, warnings, and info
    Debug = 3     // All messages including debug
}

public static class GlobalConfig
{
    public static BuildConfiguration? Config { get; set; }
    public static ILogger Logger { get; private set; } = Log.Logger;

    public static void InitializeLogging(VerbosityLevel verbosity)
    {
        var logLevel = verbosity switch
        {
            VerbosityLevel.Quiet => LogEventLevel.Error,
            VerbosityLevel.Normal => LogEventLevel.Warning,
            VerbosityLevel.Verbose => LogEventLevel.Information,
            VerbosityLevel.Debug => LogEventLevel.Debug,
            _ => LogEventLevel.Information
        };

        Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
            .CreateLogger();

        Log.Logger = Logger;
    }
}