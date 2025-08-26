using Tomlet;

namespace CryptikLemur.AssetBundleBuilder;

public static class ConfigurationLoader {
    public static BuildConfiguration LoadFromToml(string configPath, string? bundleConfigName = null,
        BuildConfiguration? cliConfig = null) {
        if (!File.Exists(configPath)) throw new FileNotFoundException($"Configuration file not found: {configPath}");

        var tomlContent = File.ReadAllText(configPath);
        var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);

        // If no bundle specified and only one bundle exists, use it
        if (string.IsNullOrEmpty(bundleConfigName)) {
            if (tomlConfig.Bundles.Count == 1) {
                bundleConfigName = tomlConfig.Bundles.Keys.First();
                GlobalConfig.Logger.Information("No bundle specified, using the only available bundle: {BundleName}",
                    bundleConfigName);
            }
            else if (tomlConfig.Bundles.Count > 1) {
                var availableBundles = string.Join(", ", tomlConfig.Bundles.Keys);
                throw new ArgumentException(
                    $"Multiple bundles found in config. Please specify which bundle to build using --bundle-config. Available bundles: {availableBundles}");
            }
            else throw new ArgumentException("No bundles found in configuration file");
        }

        if (!tomlConfig.Bundles.TryGetValue(bundleConfigName, out var bundleConfig)) {
            var availableBundles = string.Join(", ", tomlConfig.Bundles.Keys);
            throw new ArgumentException(
                $"Bundle '{bundleConfigName}' not found in configuration. Available bundles: {availableBundles}");
        }

        var globalConfig = tomlConfig.Global ?? new TomlGlobalConfig();

        // Create merged configuration with precedence: CLI > Bundle > Global > Defaults
        var mergedConfig = new BuildConfiguration();
        
        // Set sensible defaults
        mergedConfig.OutputDirectory = Directory.GetCurrentDirectory();

        // Start with global defaults
        ApplyTomlGlobalConfig(mergedConfig, globalConfig, configPath);

        // Apply bundle-specific config (overrides global)
        ApplyTomlBundleConfig(mergedConfig, bundleConfig, configPath);

        // Apply CLI config (overrides everything)
        if (cliConfig != null) ApplyCliConfig(mergedConfig, cliConfig);

        // Validate required fields
        ValidateConfiguration(mergedConfig, bundleConfigName);

        return mergedConfig;
    }

    public static List<string> ListBundles(string configPath) {
        if (!File.Exists(configPath)) throw new FileNotFoundException($"Configuration file not found: {configPath}");

        var tomlContent = File.ReadAllText(configPath);
        var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);

        return tomlConfig.Bundles.Keys.ToList();
    }

    private static void ApplyTomlGlobalConfig(BuildConfiguration config, TomlGlobalConfig globalConfig,
        string configPath) {
        if (!string.IsNullOrEmpty(globalConfig.UnityVersion)) config.UnityVersion = globalConfig.UnityVersion;
        if (!string.IsNullOrEmpty(globalConfig.UnityPath))
            config.UnityPath = ResolvePath(globalConfig.UnityPath, configPath);
        if (!string.IsNullOrEmpty(globalConfig.OutputDirectory))
            config.OutputDirectory = ResolvePath(globalConfig.OutputDirectory, configPath);
        if (!string.IsNullOrEmpty(globalConfig.BuildTarget)) config.BuildTarget = globalConfig.BuildTarget;
        if (!string.IsNullOrEmpty(globalConfig.TempProjectPath))
            config.TempProjectPath = ResolvePath(globalConfig.TempProjectPath, configPath);
        if (globalConfig.CleanTempProject.HasValue) config.CleanTempProject = globalConfig.CleanTempProject.Value;
        if (!string.IsNullOrEmpty(globalConfig.LinkMethod)) config.LinkMethod = globalConfig.LinkMethod;
        if (!string.IsNullOrEmpty(globalConfig.LogFile)) config.LogFile = ResolvePath(globalConfig.LogFile, configPath);
        if (globalConfig.CiMode.HasValue) config.CiMode = globalConfig.CiMode.Value;
        if (!string.IsNullOrEmpty(globalConfig.Verbosity)) config.Verbosity = ParseVerbosity(globalConfig.Verbosity);
        if (globalConfig.NonInteractive.HasValue) config.NonInteractive = globalConfig.NonInteractive.Value;
        if (globalConfig.ExcludePatterns != null) config.ExcludePatterns.AddRange(globalConfig.ExcludePatterns);
        if (globalConfig.IncludePatterns != null) config.IncludePatterns.AddRange(globalConfig.IncludePatterns);
    }

    private static void ApplyTomlBundleConfig(BuildConfiguration config, TomlBundleConfig bundleConfig,
        string configPath) {
        if (!string.IsNullOrEmpty(bundleConfig.AssetDirectory))
            config.AssetDirectory = ResolvePath(bundleConfig.AssetDirectory, configPath);
        if (!string.IsNullOrEmpty(bundleConfig.BundleName)) config.BundleName = bundleConfig.BundleName;
        if (!string.IsNullOrEmpty(bundleConfig.OutputDirectory))
            config.OutputDirectory = ResolvePath(bundleConfig.OutputDirectory, configPath);
        if (!string.IsNullOrEmpty(bundleConfig.UnityVersion)) config.UnityVersion = bundleConfig.UnityVersion;
        if (!string.IsNullOrEmpty(bundleConfig.UnityPath))
            config.UnityPath = ResolvePath(bundleConfig.UnityPath, configPath);
        if (!string.IsNullOrEmpty(bundleConfig.BuildTarget)) config.BuildTarget = bundleConfig.BuildTarget;
        if (!string.IsNullOrEmpty(bundleConfig.TempProjectPath))
            config.TempProjectPath = ResolvePath(bundleConfig.TempProjectPath, configPath);
        if (bundleConfig.CleanTempProject.HasValue) config.CleanTempProject = bundleConfig.CleanTempProject.Value;
        if (!string.IsNullOrEmpty(bundleConfig.LinkMethod)) config.LinkMethod = bundleConfig.LinkMethod;
        if (!string.IsNullOrEmpty(bundleConfig.LogFile)) config.LogFile = ResolvePath(bundleConfig.LogFile, configPath);
        if (bundleConfig.CiMode.HasValue) config.CiMode = bundleConfig.CiMode.Value;
        if (!string.IsNullOrEmpty(bundleConfig.Verbosity)) config.Verbosity = ParseVerbosity(bundleConfig.Verbosity);
        if (bundleConfig.NonInteractive.HasValue) config.NonInteractive = bundleConfig.NonInteractive.Value;
        if (bundleConfig.ExcludePatterns != null) config.ExcludePatterns.AddRange(bundleConfig.ExcludePatterns);
        if (bundleConfig.IncludePatterns != null) config.IncludePatterns.AddRange(bundleConfig.IncludePatterns);
        if (!string.IsNullOrEmpty(bundleConfig.Filename)) config.Filename = bundleConfig.Filename;
    }

    private static void ApplyCliConfig(BuildConfiguration mergedConfig, BuildConfiguration cliConfig) {
        if (!string.IsNullOrEmpty(cliConfig.UnityVersion)) mergedConfig.UnityVersion = cliConfig.UnityVersion;
        if (!string.IsNullOrEmpty(cliConfig.UnityPath)) mergedConfig.UnityPath = cliConfig.UnityPath;
        if (!string.IsNullOrEmpty(cliConfig.AssetDirectory)) mergedConfig.AssetDirectory = cliConfig.AssetDirectory;
        if (!string.IsNullOrEmpty(cliConfig.OutputDirectory)) mergedConfig.OutputDirectory = cliConfig.OutputDirectory;
        if (!string.IsNullOrEmpty(cliConfig.BundleName)) mergedConfig.BundleName = cliConfig.BundleName;
        if (!string.IsNullOrEmpty(cliConfig.BuildTarget)) mergedConfig.BuildTarget = cliConfig.BuildTarget;
        if (!string.IsNullOrEmpty(cliConfig.TempProjectPath)) mergedConfig.TempProjectPath = cliConfig.TempProjectPath;
        if (cliConfig.CleanTempProject) mergedConfig.CleanTempProject = cliConfig.CleanTempProject;
        if (!string.IsNullOrEmpty(cliConfig.LinkMethod) && cliConfig.LinkMethod != "copy")
            mergedConfig.LinkMethod = cliConfig.LinkMethod;
        if (!string.IsNullOrEmpty(cliConfig.LogFile)) mergedConfig.LogFile = cliConfig.LogFile;
        if (cliConfig.CiMode) mergedConfig.CiMode = cliConfig.CiMode;
        if (cliConfig.Verbosity != VerbosityLevel.Normal) mergedConfig.Verbosity = cliConfig.Verbosity;
        if (cliConfig.NonInteractive) mergedConfig.NonInteractive = cliConfig.NonInteractive;
        if (cliConfig.ExcludePatterns.Count > 0) mergedConfig.ExcludePatterns.AddRange(cliConfig.ExcludePatterns);
        if (cliConfig.IncludePatterns.Count > 0) mergedConfig.IncludePatterns.AddRange(cliConfig.IncludePatterns);
        if (!string.IsNullOrEmpty(cliConfig.Filename)) mergedConfig.Filename = cliConfig.Filename;
    }

    private static string ResolvePath(string path, string configPath) {
        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

        // Resolve relative paths relative to the config file directory
        var configDir = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(configDir, path));
    }

    private static VerbosityLevel ParseVerbosity(string verbosity) {
        return verbosity.ToLower() switch {
            "quiet" or "q" => VerbosityLevel.Quiet,
            "normal" => VerbosityLevel.Normal,
            "verbose" or "v" => VerbosityLevel.Verbose,
            "debug" or "vv" => VerbosityLevel.Debug,
            _ => throw new ArgumentException(
                $"Invalid verbosity level: {verbosity}. Valid values are: quiet, normal, verbose, debug")
        };
    }

    private static void ValidateConfiguration(BuildConfiguration config, string bundleConfigName) {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(config.AssetDirectory))
            errors.Add("AssetDirectory is required");
        else if (!Directory.Exists(config.AssetDirectory))
            errors.Add($"AssetDirectory does not exist: {config.AssetDirectory}");

        if (string.IsNullOrEmpty(config.BundleName))
            errors.Add("BundleName is required");

        if (string.IsNullOrEmpty(config.UnityVersion) && string.IsNullOrEmpty(config.UnityPath))
            errors.Add("Either UnityVersion or UnityPath is required");

        if (errors.Count > 0) {
            var errorMessage = $"Configuration validation failed for bundle '{bundleConfigName}':\n" +
                               string.Join("\n", errors.Select(e => $"  - {e}"));
            throw new ArgumentException(errorMessage);
        }
    }
}