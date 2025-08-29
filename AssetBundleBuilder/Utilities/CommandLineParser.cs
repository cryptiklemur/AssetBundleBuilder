using System.CommandLine;
using System.CommandLine.Parsing;
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

    private static RootCommand BuildRootCommand() {
        var rootCommand = new RootCommand("Unity Asset Bundle Builder for RimWorld mods");

        // Positional arguments
        var bundleConfigArg = new Argument<string?>(
            "bundle-config",
            description: "Bundle configuration name from TOML file",
            getDefaultValue: () => null) {
            Arity = ArgumentArity.ZeroOrOne
        };

        // Options
        var configOption = new Option<string?>(
            ["--config", "-c"],
            "Path to TOML configuration file");


        var targetOption = new Option<string?>(
            ["--target", "-t"],
            description: "Build target: windows, mac, linux, or none",
            getDefaultValue: () => "none");


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
                result.Tokens.Any(t => t.Value == "--help" || t.Value == "-h")) return;

            // If no arguments provided, show help
            if (result.Tokens.Count == 0) {
                context.ExitCode = rootCommand.Invoke("-h");
                return;
            }

            // Build configuration from parsed arguments
            var config = BuildConfiguration(result);

            if (config == null) {
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
            var errors = ValidateConfiguration(config);
            if (errors.Count > 0) {
                foreach (var error in errors) Console.WriteLine($"Error: {error}");
                context.ExitCode = 1;
                return;
            }

            // Initialize logging
            Program.InitializeLogging(config.GetVerbosity());

            // Execute the build
            context.ExitCode = Program.BuildAssetBundle(config);
        });

        return rootCommand;
    }

    private static Configuration? BuildConfiguration(ParseResult parseResult) {
        var config = new Configuration();

        // Check for auto-detected config file first
        var autoConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".assetbundler.toml");
        if (File.Exists(autoConfigPath) && !parseResult.HasOption("--config")) config.ConfigFile = autoConfigPath;

        // Get positional arguments
        config.BundleConfigName = parseResult.GetValue<string?>("bundle-config") ?? string.Empty;

        // Get options
        var configFile = parseResult.GetValue<string?>("--config");
        if (!string.IsNullOrEmpty(configFile)) config.ConfigFile = configFile;

        config.BuildTarget = parseResult.GetValue<string?>("--target") ?? "none";
        config.Quiet = parseResult.GetValue<bool>("--quiet");
        config.Verbose = parseResult.GetValue<bool>("--verbose");
        config.Debug = parseResult.GetValue<bool>("--debug");
        config.CiMode = parseResult.GetValue<bool>("--ci");
        config.NonInteractive = parseResult.GetValue<bool>("--non-interactive");
        config.ListBundles = parseResult.GetValue<bool>("--list-bundles");
        config.DumpConfig = parseResult.GetValue<string?>("--dump-config");


        // Load TOML configuration if specified
        if (!string.IsNullOrEmpty(config.ConfigFile)) {
            try {
                var tomlContent = File.ReadAllText(config.ConfigFile);
                var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);

                // Store original TOML config for serialization
                config.OriginalTomlConfig = tomlConfig;

                // Apply global configuration
                if (tomlConfig.Global != null) Configuration.ApplyTomlSection(config, tomlConfig.Global);

                // Auto-select bundle config if only one exists and none was specified
                if (string.IsNullOrEmpty(config.BundleConfigName) && tomlConfig.Bundles.Count == 1) {
                    config.BundleConfigName = tomlConfig.Bundles.Keys.First();
                    Console.WriteLine($"Auto-selecting bundle configuration: {config.BundleConfigName}");
                }

                // Apply specific bundle configuration if specified
                if (!string.IsNullOrEmpty(config.BundleConfigName) &&
                    tomlConfig.Bundles.ContainsKey(config.BundleConfigName)) {
                    var bundleConfig = tomlConfig.Bundles[config.BundleConfigName];
                    Configuration.ApplyTomlSection(config, bundleConfig);
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                return null;
            }
        }

        return config;
    }

    private static List<string> ValidateConfiguration(Configuration config) {
        var errors = new List<string>();

        // Basic validation - config file is required
        if (string.IsNullOrEmpty(config.ConfigFile)) {
            errors.Add(
                "Configuration file is required (use --config or place .assetbundler.toml in current directory)");
        }

        // Bundle config name is required unless we're just listing bundles
        if (string.IsNullOrEmpty(config.BundleConfigName) && !config.ListBundles)
            errors.Add("Bundle configuration name is required");

        // Validate that the bundle configuration exists in the TOML file
        if (!string.IsNullOrEmpty(config.ConfigFile) && !string.IsNullOrEmpty(config.BundleConfigName)) {
            try {
                var tomlContent = File.ReadAllText(config.ConfigFile);
                var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);
                
                if (!tomlConfig.Bundles.ContainsKey(config.BundleConfigName)) {
                    var availableBundles = string.Join(", ", tomlConfig.Bundles.Keys);
                    errors.Add($"Bundle configuration '{config.BundleConfigName}' not found in {Path.GetFileName(config.ConfigFile)}. Available bundles: {availableBundles}");
                }
            }
            catch (Exception ex) {
                errors.Add($"Error reading configuration file: {ex.Message}");
            }
        }

        // Validate build target
        var validTargets = new[] { "windows", "mac", "linux", "none" };
        if (!string.IsNullOrEmpty(config.BuildTarget) && !validTargets.Contains(config.BuildTarget))
            errors.Add($"Invalid build target: \"{config.BuildTarget}\". Valid values are: windows, mac, linux, none");
        
        // If targetless is false, require a proper build target (not "none")
        if (!config.Targetless && config.BuildTarget == "none") {
            errors.Add("Bundle requires a specific build target (targetless is false). Please specify --target with windows, mac, or linux");
        }


        // Validate bundle name format
        if (!string.IsNullOrEmpty(config.BundleName)) {
            var lowerBundleName = config.BundleName.ToLower();
            if (lowerBundleName.EndsWith(".framework") || lowerBundleName.EndsWith(".bundle"))
                errors.Add($"Bundle name cannot end with .framework or .bundle: {lowerBundleName}");
        }

        return errors;
    }

    private static void ListBundlesInConfig(string configPath) {
        if (!File.Exists(configPath)) {
            Console.WriteLine($"Config file not found: {configPath}");
            return;
        }

        try {
            var content = File.ReadAllText(configPath);
            var config = TomletMain.To<TomlConfiguration>(content);

            if (config.Bundles.Count == 0) {
                Console.WriteLine("No bundles defined in config file");
                return;
            }

            Console.WriteLine($"Available bundles in {Path.GetFileName(configPath)}:");
            Console.WriteLine();
            foreach (var bundle in config.Bundles) {
                Console.Write($"{bundle.Key}");
                if (!string.IsNullOrEmpty(bundle.Value.Description)) Console.Write($" - {bundle.Value.Description}");
                Console.WriteLine();
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error reading config file: {ex.Message}");
        }
    }

    private static bool HasOption(this ParseResult parseResult, string alias) {
        return parseResult.Tokens.Any(t => t.Value == alias);
    }

    private static T? GetValue<T>(this ParseResult parseResult, string name) {
        // Try to get from options first
        foreach (var option in parseResult.CommandResult.Command.Options)
            if (option.Aliases.Contains(name) || option.Name == name)
                return parseResult.GetValueForOption(option) is T value ? value : default;

        // Then try arguments
        foreach (var argument in parseResult.CommandResult.Command.Arguments)
            if (argument.Name == name)
                return parseResult.GetValueForArgument(argument) is T value ? value : default;

        return default;
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