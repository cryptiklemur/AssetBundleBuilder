using Serilog;
using Serilog.Events;

namespace CryptikLemur.AssetBundleBuilder;

public enum VerbosityLevel {
    Quiet = 0, // Only errors
    Normal = 1, // Errors and warnings
    Verbose = 2, // Errors, warnings, and info
    Debug = 3 // All messages including debug
}

public static class GlobalConfig {
    public static BuildConfiguration? Config { get; set; }
    public static ILogger Logger { get; private set; } = CreateDefaultLogger();

    private static ILogger CreateDefaultLogger() {
        try {
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
                .CreateLogger();
        }
        catch {
            // Fallback to a minimal logger if console setup fails
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .CreateLogger();
        }
    }

    public static void InitializeLogging(VerbosityLevel verbosity) {
        try {
            var logLevel = verbosity switch {
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
        catch {
            // If logging initialization fails, keep the existing logger
            // This ensures we never break the application due to logging issues
        }
    }
}