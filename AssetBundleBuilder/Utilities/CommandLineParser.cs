using System.CommandLine;
using CryptikLemur.AssetBundleBuilder.Config;
using Tomlet;

namespace CryptikLemur.AssetBundleBuilder.Utilities;

/// <summary>
///     Modern CLI parser using System.CommandLine
/// </summary>
public static class CommandLineParser {
    public static async Task<int> ParseAndExecuteAsync(string[] args) {
        var rootCommand = BuildRootCommand();
        return await rootCommand.InvokeAsync(args);
    }

    public static RootCommand BuildRootCommand() {
        var rootCommand = new RootCommand("Unity Asset Bundle Builder for RimWorld mods");

        // Positional arguments
        var bundleConfigArg = new Argument<string[]>(
            "bundle-config",
            description: "Bundle configuration names from TOML file (can specify multiple)",
            getDefaultValue: () => []) {
            Arity = ArgumentArity.ZeroOrMore
        };

        // Options
        var configOption = new Option<string?>(
            ["--config", "-c"],
            "Path to TOML configuration file");


        var targetOption = new Option<string[]>(
            ["--target", "-t"],
            description: "Build targets: windows, mac, linux (defaults to all three)",
            getDefaultValue: () => ["windows", "mac", "linux"]) {
            AllowMultipleArgumentsPerToken = true
        };


        var quietOption = new Option<bool>(
            ["--quiet", "-q"],
            "Minimal output");

        var verboseOption = new Option<bool>(
            ["--verbose", "-v"],
            "Verbose output");

        var debugOption = new Option<bool>(
            ["--debug", "-vv"],
            "Debug output");

        var ciOption = new Option<bool>(
            ["--ci"],
            "CI mode - disable automatic installations");

        var nonInteractiveOption = new Option<bool>(
            ["--non-interactive"],
            "Non-interactive mode for automated environments");

        var listBundlesOption = new Option<bool>(
            ["--list-bundles"],
            "List available bundles in config file");

        var dumpConfigOption = new Option<string?>(
            ["--dump-config"],
            "Dump resolved configuration in specified format (json|toml)");

        // Add all arguments and options to root command
        rootCommand.AddArgument(bundleConfigArg);

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(targetOption);
        rootCommand.AddOption(quietOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(debugOption);
        rootCommand.AddOption(ciOption);
        rootCommand.AddOption(nonInteractiveOption);
        rootCommand.AddOption(listBundlesOption);
        rootCommand.AddOption(dumpConfigOption);

        // Set the handler
        rootCommand.SetHandler(context => {
            var result = context.ParseResult;

            // Check if help was invoked - if so, System.CommandLine already handled it, just exit
            if (result.CommandResult.Command == rootCommand &&
                result.Tokens.Any(t => t.Value == "--help" || t.Value == "-h")) {
                return;
            }

            // If no arguments provided, try to use default config file
            if (result.Tokens.Count == 0) {
                string defaultConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".assetbundler.toml");
                if (!File.Exists(defaultConfigPath)) {
                    Console.WriteLine("No .assetbundler.toml found in current directory.");
                    Console.WriteLine(
                        "Use --help for usage information or create a .assetbundler.toml configuration file.");
                    context.ExitCode = 1;
                    return;
                }
            }

            // Build configuration from parsed arguments
            Configuration config;
            try {
                config = new Configuration(result);
            }
            catch (Exception ex) {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                context.ExitCode = 1;
                return;
            }

            if (string.IsNullOrEmpty(config.ConfigFile)) {
                Console.WriteLine(
                    "Error: Configuration file is required (use --config or place .assetbundler.toml in current directory)");
                context.ExitCode = 1;
                return;
            }

            // Handle special commands first, before validation
            if (config.ListBundles) {
                ListBundlesInConfig(config.ConfigFile);
                context.ExitCode = 0;
                return;
            }

            if (!string.IsNullOrEmpty(config.DumpConfig)) {
                DumpConfigInFormat(config, config.DumpConfig);
                context.ExitCode = 0;
                return;
            }

            // Validate configuration
            var errors = config.Validate();
            if (errors.Count > 0) {
                foreach (string error in errors) {
                    Console.WriteLine($"Error: {error}");
                }

                context.ExitCode = 1;
                return;
            }

            // Initialize logging
            Program.InitializeLogging(config.GetVerbosity());

            Program.Logger.Verbose(
                "No specific bundles specified, building all {Count} bundles", config.BundleConfigNames.Count);

            // Execute the build using multi-bundle mode (works for single bundles too)
            context.ExitCode = Program.BuildAssetBundles(config).GetAwaiter().GetResult();
        });

        return rootCommand;
    }

    private static void ListBundlesInConfig(string configPath) {
        if (!File.Exists(configPath)) {
            Console.WriteLine($"Config file not found: {configPath}");
            return;
        }

        try {
            string content = File.ReadAllText(configPath);
            var config = TomletMain.To<TomlConfiguration>(content);

            if (config.Bundles.Count == 0) {
                Console.WriteLine("No bundles defined in config file");
                return;
            }

            Console.WriteLine($"Available bundles in {Path.GetFileName(configPath)}:");
            Console.WriteLine();
            foreach (var bundle in config.Bundles) {
                Console.Write($"{bundle.Key}");
                if (!string.IsNullOrEmpty(bundle.Value.Description)) {
                    Console.Write($" - {bundle.Value.Description}");
                }

                Console.WriteLine();
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error reading config file: {ex.Message}");
        }
    }

    private static void DumpConfigInFormat(Configuration config, string format) {
        try {
            string output = format.ToLower() switch {
                "json" => config.ToJson(),
                "toml" => config.ToToml(),
                _ => throw new ArgumentException($"Unsupported format '{format}'. Supported formats: json, toml")
            };

            Console.WriteLine(output);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error dumping config in {format} format: {ex.Message}");
        }
    }
}