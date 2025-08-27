using CryptikLemur.AssetBundleBuilder.Config;
using Tomlet;

namespace CryptikLemur.AssetBundleBuilder.Utilities;

/// <summary>
///     ArgumentParser that uses the Configuration attribute-based system
/// </summary>
public static class ArgumentParser {
    public static Configuration? Parse(string[] args) {
        try {
            var globalConfig = Configuration.ParseArguments(args);

            // Handle special cases like --list-bundles
            if (globalConfig.ListBundles && !string.IsNullOrEmpty(globalConfig.ConfigFile)) {
                ListBundlesInConfig(globalConfig.ConfigFile);
                return null; // Signal that we handled a special command
            }

            // If we have a config file, load and merge it
            if (!string.IsNullOrEmpty(globalConfig.ConfigFile)) {
                try {
                    // Load TOML configuration and apply it to the current config
                    var tomlContent = File.ReadAllText(globalConfig.ConfigFile);
                    var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);

                    // Apply global configuration if present
                    if (tomlConfig.Global != null) Configuration.ApplyTomlSection(globalConfig, tomlConfig.Global);

                    // Apply specific bundle configuration if specified
                    if (!string.IsNullOrEmpty(globalConfig.BundleConfigName) &&
                        tomlConfig.Bundles.ContainsKey(globalConfig.BundleConfigName)) {
                        var bundleConfig = tomlConfig.Bundles[globalConfig.BundleConfigName];
                        Configuration.ApplyTomlSection(globalConfig, bundleConfig);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error loading configuration: {ex.Message}");
                    return null;
                }
            }

            // Validate configuration
            var errors = ValidateConfiguration(globalConfig);
            if (errors.Count > 0) {
                // Check if there's a bundle name validation error and it's a forbidden extension
                var bundleNameError = errors.FirstOrDefault(e => e.Contains("cannot end with .framework or .bundle"));
                if (bundleNameError != null)
                    throw new ArgumentException(bundleNameError);

                // For other validation errors (like missing required fields), return null
                foreach (var error in errors) Console.WriteLine($"Error parsing arguments: {error}");
                return null;
            }

            return globalConfig;
        }
        catch (ArgumentException) {
            // Re-throw ArgumentExceptions (like bundle name validation errors)
            throw;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error parsing arguments: {ex.Message}");
            return null;
        }
    }

    private static List<string> ValidateConfiguration(Configuration config) {
        var errors = new List<string>();

        // Basic validation (only if not using config file)
        if (string.IsNullOrEmpty(config.ConfigFile)) {
            if (string.IsNullOrEmpty(config.BundleName) && string.IsNullOrEmpty(config.BundleNameOverride))
                errors.Add("Bundle name is required");

            if (string.IsNullOrEmpty(config.AssetDirectory)) errors.Add("Asset directory is required");

            if (string.IsNullOrEmpty(config.UnityVersionOrPath) &&
                string.IsNullOrEmpty(config.UnityVersion) &&
                string.IsNullOrEmpty(config.UnityPath)) errors.Add("Unity version or path is required");
        }

        // Validate build target
        if (!string.IsNullOrEmpty(config.BuildTarget)) {
            var validTargets = new[] { "", "windows", "mac", "linux" };
            if (!validTargets.Contains(config.BuildTarget))
                errors.Add($"Invalid build target: {config.BuildTarget}. Valid values are: windows, mac, linux");
        }

        // Check link method conflicts
        var linkMethodCount = 0;
        if (config.UseCopy) linkMethodCount++;
        if (config.UseSymlink) linkMethodCount++;
        if (config.UseHardlink) linkMethodCount++;
        if (config.UseJunction) linkMethodCount++;

        if (linkMethodCount > 1) errors.Add("Only one link method can be specified at a time");

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

            if (config.Bundles == null || config.Bundles.Count == 0) {
                Console.WriteLine("No bundles defined in config file");
                return;
            }

            Console.WriteLine($"Available bundles in {Path.GetFileName(configPath)}:");
            foreach (var bundle in config.Bundles) {
                Console.WriteLine($"  {bundle.Key}");
                if (!string.IsNullOrEmpty(bundle.Value.Description))
                    Console.WriteLine($"    {bundle.Value.Description}");
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error reading config file: {ex.Message}");
        }
    }
}