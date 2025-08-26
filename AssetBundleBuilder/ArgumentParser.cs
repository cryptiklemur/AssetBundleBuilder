namespace CryptikLemur.AssetBundleBuilder;

public static class ArgumentParser {
    public static BuildConfiguration? Parse(string[] args) {
        // First, check for automatic .assetbundler.toml detection
        var autoConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".assetbundler.toml");
        var hasAutoConfig = File.Exists(autoConfigPath);
        
        // Check for explicit --config argument
        var explicitConfigPath = GetExplicitConfigPath(args);
        
        // If we have a config file (auto or explicit), use config-first mode
        if (hasAutoConfig || !string.IsNullOrEmpty(explicitConfigPath)) {
            var configPath = explicitConfigPath ?? autoConfigPath;
            var configFirstConfig = new BuildConfiguration { ConfigFile = configPath };
            
            // Parse all CLI arguments for potential overrides
            ParseConfigModeArguments(args, configFirstConfig);
            
            return configFirstConfig;
        }

        // Check for config-only mode (--config as first argument)
        if (args.Length >= 2 && args[0] == "--config") {
            var configPath = Path.GetFullPath(args[1]);
            var configOnlyConfig = new BuildConfiguration { ConfigFile = configPath };

            // Look for --bundle-config in the remaining args
            for (var i = 2; i < args.Length; i++)
                if (args[i] == "--bundle-config" && i + 1 < args.Length) {
                    configOnlyConfig.BundleConfigName = args[i + 1];
                    break;
                }

            return configOnlyConfig;
        }

        if (args.Length < 3 && !HasConfigFile(args)) return null;

        var config = new BuildConfiguration();
        int argIndex;

        if (args[0] == "--unity-version" && args.Length >= 4) {
            config.UnityVersion = args[1];
            config.AssetDirectory = args[2];
            config.BundleName = args[3];

            // Optional output directory
            if (args.Length >= 5 && !args[4].StartsWith("--")) {
                config.OutputDirectory = args[4];
                argIndex = 5;
            }
            else {
                config.OutputDirectory = Directory.GetCurrentDirectory();
                argIndex = 4;
            }
        }
        else if (args.Length >= 3) {
            var firstArg = args[0];
            if (File.Exists(firstArg) && (firstArg.EndsWith("Unity.exe") || firstArg.EndsWith("Unity")))
                config.UnityPath = firstArg;
            else
                config.UnityVersion = firstArg;

            config.AssetDirectory = args[1];
            config.BundleName = args[2];

            // Optional output directory
            if (args.Length >= 4 && !args[3].StartsWith("--")) {
                config.OutputDirectory = args[3];
                argIndex = 4;
            }
            else {
                config.OutputDirectory = Directory.GetCurrentDirectory();
                argIndex = 3;
            }
        }
        else return null;

        for (var i = argIndex; i < args.Length; i++)
            switch (args[i]) {
                case "--bundle-name" when i + 1 < args.Length:
                    config.BundleName = args[++i];
                    break;
                case "--target" when i + 1 < args.Length: {
                    config.BuildTarget = args[++i];
                    if (config.BuildTarget != "windows" && config.BuildTarget != "mac" &&
                        config.BuildTarget != "linux")
                        return null;

                    break;
                }
                case "--temp-project" when i + 1 < args.Length:
                    config.TempProjectPath = Path.GetFullPath(args[++i]);
                    break;
                case "--unity-version" when i + 1 < args.Length:
                    config.UnityVersion = args[++i];
                    break;
                case "--clean-temp":
                    config.CleanTempProject = true;
                    break;
                case "--copy":
                    config.LinkMethod = "copy";
                    break;
                case "--symlink":
                    config.LinkMethod = "symlink";
                    break;
                case "--hardlink":
                    config.LinkMethod = "hardlink";
                    break;
                case "--junction":
                    config.LinkMethod = "junction";
                    break;
                case "--logfile" when i + 1 < args.Length:
                    config.LogFile = Path.GetFullPath(args[++i]);
                    break;
                case "--ci":
                    config.CiMode = true;
                    break;
                case "-v":
                case "--verbose":
                    config.Verbosity = VerbosityLevel.Verbose;
                    break;
                case "-vv":
                case "--debug":
                    config.Verbosity = VerbosityLevel.Debug;
                    break;
                case "-q":
                case "--quiet":
                    config.Verbosity = VerbosityLevel.Quiet;
                    break;
                case "--non-interactive":
                    config.NonInteractive = true;
                    break;
                case "--exclude" when i + 1 < args.Length:
                    config.ExcludePatterns.Add(args[++i]);
                    break;
                case "--include" when i + 1 < args.Length:
                    config.IncludePatterns.Add(args[++i]);
                    break;
                case "--filename" when i + 1 < args.Length:
                    config.Filename = args[++i];
                    break;
                case "--config" when i + 1 < args.Length:
                    config.ConfigFile = Path.GetFullPath(args[++i]);
                    break;
                case "--bundle-config" when i + 1 < args.Length:
                    config.BundleConfigName = args[++i];
                    break;
            }

        if (string.IsNullOrEmpty(config.BundleName)) return null;

        config.BundleName = config.BundleName.ToLower().Replace(" ", "");

        // Validate bundle name doesn't end with forbidden extensions
        if (config.BundleName.EndsWith(".framework") || config.BundleName.EndsWith(".bundle"))
            throw new ArgumentException($"Bundle name '{config.BundleName}' cannot end with .framework or .bundle");

        config.AssetDirectory = Path.GetFullPath(config.AssetDirectory);
        config.OutputDirectory = Path.GetFullPath(config.OutputDirectory);

        // Auto-detect CI environment
        if (!config.CiMode && Environment.GetEnvironmentVariable("CI") == "true") config.CiMode = true;

        return config;
    }

    private static bool HasConfigFile(string[] args) {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--config")
                return true;

        return false;
    }

    private static string? GetExplicitConfigPath(string[] args) {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--config")
                return Path.GetFullPath(args[i + 1]);
        return null;
    }

    private static void ParseConfigModeArguments(string[] args, BuildConfiguration config) {
        var positionalIndex = 0;
        var skipNext = false;
        
        for (var i = 0; i < args.Length; i++) {
            if (skipNext) {
                skipNext = false;
                continue;
            }
            
            switch (args[i]) {
                case "--config" when i + 1 < args.Length:
                    // Skip the config file path, already processed
                    skipNext = true;
                    break;
                case "--bundle-config" when i + 1 < args.Length:
                    config.BundleConfigName = args[++i];
                    break;
                case "--unity-version" when i + 1 < args.Length:
                    config.UnityVersion = args[++i];
                    break;
                case "--target" when i + 1 < args.Length:
                    var target = args[++i];
                    if (target == "windows" || target == "mac" || target == "linux")
                        config.BuildTarget = target;
                    break;
                case "--temp-project" when i + 1 < args.Length:
                    config.TempProjectPath = Path.GetFullPath(args[++i]);
                    break;
                case "--clean-temp":
                    config.CleanTempProject = true;
                    break;
                case "--copy":
                    config.LinkMethod = "copy";
                    break;
                case "--symlink":
                    config.LinkMethod = "symlink";
                    break;
                case "--hardlink":
                    config.LinkMethod = "hardlink";
                    break;
                case "--junction":
                    config.LinkMethod = "junction";
                    break;
                case "--logfile" when i + 1 < args.Length:
                    config.LogFile = Path.GetFullPath(args[++i]);
                    break;
                case "--ci":
                    config.CiMode = true;
                    break;
                case "-v" or "--verbose":
                    config.Verbosity = VerbosityLevel.Verbose;
                    break;
                case "-vv" or "--debug":
                    config.Verbosity = VerbosityLevel.Debug;
                    break;
                case "-q" or "--quiet":
                    config.Verbosity = VerbosityLevel.Quiet;
                    break;
                case "--non-interactive":
                    config.NonInteractive = true;
                    break;
                case "--exclude" when i + 1 < args.Length:
                    config.ExcludePatterns.Add(args[++i]);
                    break;
                case "--include" when i + 1 < args.Length:
                    config.IncludePatterns.Add(args[++i]);
                    break;
                case "--filename" when i + 1 < args.Length:
                    config.Filename = args[++i];
                    break;
                case "--bundle-name" when i + 1 < args.Length:
                    config.BundleName = args[++i];
                    break;
                default:
                    // Handle positional arguments as CLI overrides
                    if (!args[i].StartsWith("--")) {
                        switch (positionalIndex) {
                            case 0: // Unity version or path
                                if (File.Exists(args[i]) && (args[i].EndsWith("Unity.exe") || args[i].EndsWith("Unity")))
                                    config.UnityPath = args[i];
                                else
                                    config.UnityVersion = args[i];
                                break;
                            case 1: // Asset directory
                                config.AssetDirectory = args[i];
                                break;
                            case 2: // Bundle name
                                config.BundleName = args[i];
                                break;
                            case 3: // Output directory
                                config.OutputDirectory = args[i];
                                break;
                        }
                        positionalIndex++;
                    }
                    break;
            }
        }
        
        // Auto-detect CI environment
        if (!config.CiMode && Environment.GetEnvironmentVariable("CI") == "true") config.CiMode = true;
    }
}