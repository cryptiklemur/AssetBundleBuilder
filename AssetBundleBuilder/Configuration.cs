using System.Reflection;
using System.Text.RegularExpressions;
using CryptikLemur.AssetBundleBuilder.Attributes;
using CryptikLemur.AssetBundleBuilder.Config;
using Tomlet;

namespace CryptikLemur.AssetBundleBuilder;

/// <summary>
///     Global configuration that combines CLI arguments and TOML configuration
/// </summary>
public class Configuration {
    // Positional Arguments (order matters)
    [Cli(0, "Unity version (e.g., 2022.3.35f1) or path to Unity executable")]
    public string UnityVersionOrPath { get; set; } = string.Empty;

    [Cli(1, "Path to the directory containing assets to bundle")]
    [Toml("asset_directory")]
    public string AssetDirectory { get; set; } = string.Empty;

    [Cli(2, "Name for the asset bundle (e.g., author.modname)")]
    [Toml("bundle_name")]
    public string BundleName { get; set; } = string.Empty;

    [Cli(3, "Output directory (optional, defaults to current directory)")]
    [Toml("output_directory")]
    public string OutputDirectory { get; set; } = string.Empty;

    // Named Arguments
    [Cli("unity-version", description: "Specify Unity version explicitly")]
    [Toml("unity_version")]
    public string? UnityVersion { get; set; }

    [Cli("unity-path", description: "Path to Unity executable")]
    [Toml("unity_path")]
    public string? UnityPath { get; set; }

    [Cli("target", description: "Build target: windows, mac, or linux")]
    [Toml("build_target")]
    public string BuildTarget { get; set; } = string.Empty;

    // Allowed build targets (if specified, restricts which targets can be built)
    [Toml("build_targets")] public List<string> BuildTargets { get; set; } = ["windows", "mac", "linux"];

    [Cli("bundle-name", description: "Override bundle name")]
    [Toml("bundle_name_override")]
    public string? BundleNameOverride { get; set; }

    [Cli("filename", description: "Custom filename format with variables")]
    [Toml("filename")]
    public string Filename { get; set; } = string.Empty;

    [Cli("temp-project", description: "Custom path for temporary Unity project")]
    [Toml("temp_project_path")]
    public string TempProjectPath { get; set; } = string.Empty;

    [Cli("clean-temp", "c", "Delete temporary project after build")]
    [Toml("clean_temp_project")]
    public bool CleanTempProject { get; set; } = false;

    // Link methods (CLI flags map to single LinkMethod property)
    [Cli("copy", description: "Copy assets to Unity project")]
    public bool UseCopy { get; set; }

    [Cli("symlink", description: "Create symbolic link to assets")]
    public bool UseSymlink { get; set; }

    [Cli("hardlink", description: "Create hard links to assets")]
    public bool UseHardlink { get; set; }

    [Cli("junction", description: "Create directory junction (Windows only)")]
    public bool UseJunction { get; set; }

    [Toml("link_method")] public string LinkMethod { get; set; } = string.Empty;

    // Logging
    [Cli("logfile", description: "Write Unity log to specified file")]
    [Toml("log_file")]
    public string LogFile { get; set; } = string.Empty;

    [Cli("quiet", "q", "Show only errors")]
    public bool Quiet { get; set; }

    [Cli("verbose", "v", "Show info, warnings, and errors")]
    public bool Verbose { get; set; }

    [Cli("debug", "vv", "Show all messages including debug")]
    public bool Debug { get; set; }

    [Toml("verbosity")] public string? VerbosityString { get; set; }

    // Patterns
    [Cli("exclude", description: "Exclude files matching pattern (repeatable)")]
    [Toml("exclude_patterns")]
    public List<string> ExcludePatterns { get; set; } = [];

    [Cli("include", description: "Include only files matching pattern (repeatable)")]
    [Toml("include_patterns")]
    public List<string> IncludePatterns { get; set; } = [];

    // Automation
    [Cli("ci", description: "CI mode (no prompts, no auto-install)")]
    [Toml("ci_mode")]
    public bool CiMode { get; set; }

    [Cli("non-interactive", description: "Auto-answer yes, exit on errors")]
    [Toml("non_interactive")]
    public bool NonInteractive { get; set; }

    // Configuration file support
    [Cli("config", description: "Use TOML configuration file")]
    public string ConfigFile { get; set; } = string.Empty;

    [Cli("bundle-config", description: "Select specific bundle from config")]
    public string BundleConfigName { get; set; } = string.Empty;

    [Cli("list-bundles", description: "List available bundles in config")]
    public bool ListBundles { get; set; }

    /// <summary>Indicates if the bundle should not have a platform suffix (targetless bundle)</summary>
    [Toml("targetless")]
    public bool Targetless { get; set; } = true;

    // Computed properties
    public VerbosityLevel GetVerbosity() {
        if (Quiet) return VerbosityLevel.Quiet;
        if (Debug) return VerbosityLevel.Debug;
        if (Verbose) return VerbosityLevel.Verbose;

        if (!string.IsNullOrEmpty(VerbosityString)) {
            return VerbosityString.ToLower() switch {
                "quiet" or "q" => VerbosityLevel.Quiet,
                "verbose" or "v" => VerbosityLevel.Verbose,
                "debug" or "vv" => VerbosityLevel.Debug,
                _ => VerbosityLevel.Normal
            };
        }

        return VerbosityLevel.Normal;
    }

    public string GetLinkMethod() {
        if (UseJunction) return "junction";
        if (UseHardlink) return "hardlink";
        if (UseSymlink) return "symlink";
        if (UseCopy) return "copy";

        return LinkMethod;
    }


    private static void ApplyTomlConfiguration(Configuration config) {
        if (!File.Exists(config.ConfigFile))
            throw new FileNotFoundException($"Config file not found: {config.ConfigFile}");

        var tomlContent = File.ReadAllText(config.ConfigFile);
        var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);

        // Apply global configuration if present
        if (tomlConfig.Global != null) ApplyTomlSection(config, tomlConfig.Global);

        // Apply specific bundle configuration if specified
        if (!string.IsNullOrEmpty(config.BundleConfigName) &&
            tomlConfig.Bundles.TryGetValue(config.BundleConfigName, out var bundleConfig))
            ApplyTomlSection(config, bundleConfig);
    }

    public static void ApplyTomlSection(Configuration config, object tomlSection) {
        var configProps = typeof(Configuration).GetProperties();
        var sectionProps = tomlSection.GetType().GetProperties();

        foreach (var sectionProp in sectionProps) {
            var value = sectionProp.GetValue(tomlSection);
            if (value == null) continue;

            // Find matching config property by TomlAttribute
            foreach (var configProp in configProps) {
                var tomlAttr = configProp.GetCustomAttribute<TomlAttribute>();

                // Match by property name - the TomlProperty attribute parameter IS the name
                if (tomlAttr == null || sectionProp.Name == null) continue;

                // Convert section property name to snake_case to match TOML convention
                var sectionPropName = ConvertToSnakeCase(sectionProp.Name);

                if (tomlAttr.Name != sectionPropName &&
                    configProp.Name != sectionProp.Name) continue;

                // Don't override CLI values with TOML values for certain properties
                var currentValue = configProp.GetValue(config);

                // Special handling for List<string> properties
                if (configProp.PropertyType == typeof(List<string>) && value is List<string> newList) {
                    // For BuildTargets, replace the default list entirely (it's a restriction, not additive)
                    if (configProp.Name == nameof(BuildTargets)) configProp.SetValue(config, newList);
                    else {
                        // For other lists, merge instead of replace
                        var currentList = currentValue as List<string> ?? [];
                        // Only apply if current list is empty (default) or we're merging
                        if (currentList.Count == 0) configProp.SetValue(config, newList);
                        else {
                            // Merge lists - add items from TOML that aren't already in the list
                            foreach (var item in newList.Where(item => !currentList.Contains(item)))
                                currentList.Add(item);
                        }
                    }
                }
                else {
                    // For non-list properties, check if current value is default
                    var isDefault = IsDefaultValue(currentValue);
                    if (currentValue != null && !isDefault) continue; // CLI value takes precedence

                    configProp.SetValue(config, value);
                }

                break;
            }
        }
    }

    private static bool IsDefaultValue(object? value) {
        return value switch {
            string s => string.IsNullOrEmpty(s),
            bool b => true, // Always consider bool values as "default" so TOML values are applied
            List<string> list => list.Count == 0,
            _ => false
        };
    }

    private static string ConvertToSnakeCase(string input) {
        // Convert PascalCase/camelCase to snake_case
        var result = Regex.Replace(
            input,
            "([a-z0-9])([A-Z])",
            "$1_$2");
        return result.ToLower();
    }

    /// <summary>
    ///     Get resolved Unity path from the configuration
    /// </summary>
    public string GetUnityPath() {
        if (!string.IsNullOrEmpty(UnityPath)) return UnityPath;

        if (!string.IsNullOrEmpty(UnityVersionOrPath) &&
            File.Exists(UnityVersionOrPath) &&
            (UnityVersionOrPath.EndsWith("Unity.exe") || UnityVersionOrPath.EndsWith("Unity")))
            return UnityVersionOrPath;

        return "";
    }

    /// <summary>
    ///     Get resolved Unity version from the configuration
    /// </summary>
    public string GetUnityVersion() {
        if (!string.IsNullOrEmpty(UnityVersion)) return UnityVersion;

        if (!string.IsNullOrEmpty(UnityVersionOrPath) &&
            !File.Exists(UnityVersionOrPath)) return UnityVersionOrPath;

        return "";
    }

    /// <summary>
    ///     Get resolved bundle name (with override if specified)
    /// </summary>
    public string GetBundleName() {
        return BundleNameOverride ?? BundleName;
    }

    /// <summary>
    ///     Get resolved output directory (with default if empty)
    /// </summary>
    public string GetOutputDirectory() {
        if (string.IsNullOrEmpty(OutputDirectory)) return Directory.GetCurrentDirectory();

        // Ensure output directory is always absolute path
        // This prevents Unity from resolving it relative to the temp project directory
        return Path.IsPathRooted(OutputDirectory)
            ? OutputDirectory
            : Path.GetFullPath(OutputDirectory);
    }

    /// <summary>
    ///     Check if this is a targetless (platform-agnostic) build
    /// </summary>
    public bool IsTargetless() {
        // A bundle is targetless if explicitly set to targetless=true AND the target is "none"
        return Targetless && BuildTarget.Equals("none", StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    ///     Check if the current build target is allowed based on build_targets configuration
    /// </summary>
    public bool IsBuildTargetAllowed() {
        // If no build_targets specified, all targets are allowed
        if (BuildTargets.Count == 0) return true;

        // If no specific build target is set, it's allowed (will use default)
        if (string.IsNullOrEmpty(BuildTarget)) return true;

        // "none" is always allowed for targetless builds
        return IsTargetless() ||
               // Check if the current build target is in the allowed list (case-insensitive)
               BuildTargets.Any(t => string.Equals(t, BuildTarget, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Get a message explaining why the build target was skipped
    /// </summary>
    public string GetBuildTargetSkipMessage() {
        if (BuildTargets.Count == 0) return string.Empty;

        var allowedTargets = string.Join(", ", BuildTargets);
        return $"Skipping bundle build: Target '{BuildTarget}' is not in allowed targets [{allowedTargets}]";
    }
}