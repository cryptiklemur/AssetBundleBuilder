using System.CommandLine.Parsing;
using System.Text.Json;
using CryptikLemur.AssetBundleBuilder.Config;
using CryptikLemur.AssetBundleBuilder.Extensions;
using CryptikLemur.AssetBundleBuilder.Utilities;
using Tomlet;

namespace CryptikLemur.AssetBundleBuilder;

/// <summary>
///     Global configuration that combines CLI arguments and TOML configuration
/// </summary>
public class Configuration {
    private static readonly List<string> ValidTargets = ["windows", "mac", "linux"];

    public Configuration() {
    }

    public Configuration(ParseResult parseResult) {
        // Check for auto-detected config file first
        string autoConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".assetbundler.toml");
        if (File.Exists(autoConfigPath) && !parseResult.HasOption("--config")) {
            ConfigFile = autoConfigPath;
        }

        // Get positional arguments (array)
        string[] bundleConfigNames = parseResult.GetValue<string[]>("bundle-config") ?? [];
        BundleConfigNames = bundleConfigNames.ToList();

        // Get options
        string? configFile = parseResult.GetValue<string?>("--config");
        if (!string.IsNullOrEmpty(configFile)) {
            ConfigFile = configFile;
        }

        // Handle multiple targets
        string[] buildTargets = parseResult.GetValue<string[]>("--target") ?? [];
        BuildTargetList = buildTargets.ToList();

        Quiet = parseResult.GetValue<bool>("--quiet");
        Verbose = parseResult.GetValue<bool>("--verbose");
        Debug = parseResult.GetValue<bool>("--debug");
        CiMode = parseResult.GetValue<bool>("--ci");
        NonInteractive = parseResult.GetValue<bool>("--non-interactive");
        ListBundles = parseResult.GetValue<bool>("--list-bundles");
        DumpConfig = parseResult.GetValue<string?>("--dump-config");

        // Load TOML configuration if specified
        if (string.IsNullOrEmpty(ConfigFile)) {
            return;
        }

        string tomlContent = File.ReadAllText(ConfigFile);
        var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);

        // Store original TOML config for serialization
        TomlConfig = tomlConfig;

        if (string.IsNullOrEmpty(TomlConfig.Global.UnityHubPath)) {
            TomlConfig.Global.UnityHubPath =
                UnityPathFinder.GetUnityHubExecutablePath(this) ?? string.Empty;
        }

        if (string.IsNullOrEmpty(TomlConfig.Global.UnityEditorPath)) {
            TomlConfig.Global.UnityEditorPath = UnityPathFinder.FindUnityExecutable(this);
        }

        // Auto-select bundle config if only one exists and none was specified
        if (BundleConfigNames.Count == 0 && tomlConfig.Bundles.Count == 1) {
            string singleBundle = tomlConfig.Bundles.Keys.First();
            BundleConfigNames = [singleBundle];
            Console.WriteLine($"Auto-selecting bundle configuration: {singleBundle}");
        }

        // If no bundles specified, build all available bundles
        if (BundleConfigNames.Count != 0 || tomlConfig.Bundles.Count <= 1 || DumpConfig != null || ListBundles) {
            return;
        }

        BundleConfigNames = tomlConfig.Bundles.Keys.ToList();
    }

    public List<string> BundleConfigNames { get; set; } = [];
    public List<string> BuildTargetList { get; set; } = [];
    public bool Quiet { get; set; }
    public bool Verbose { get; set; }
    public bool Debug { get; set; }
    public bool CiMode { get; set; }
    public bool NonInteractive { get; set; }
    public string ConfigFile { get; set; } = string.Empty;

    public bool ListBundles { get; set; }
    public string? DumpConfig { get; set; }

    public TomlConfiguration TomlConfig { get; set; } = null!;

    // Computed properties
    public VerbosityLevel GetVerbosity() {
        if (Quiet) {
            return VerbosityLevel.Quiet;
        }

        if (Debug) {
            return VerbosityLevel.Debug;
        }

        if (Verbose) {
            return VerbosityLevel.Verbose;
        }

        return VerbosityLevel.Normal;
    }

    public List<string> Validate() {
        var errors = new List<string>();

        // Basic validation - config file is required
        if (string.IsNullOrEmpty(ConfigFile)) {
            errors.Add(
                "Configuration file is required (use --config or place .assetbundler.toml in current directory)");
        }

        // Validate that specified bundle configurations exist in the TOML file
        if (!string.IsNullOrEmpty(ConfigFile) && BundleConfigNames.Count > 0) {
            try {
                if (string.IsNullOrEmpty(TomlConfig.Global.UnityVersion)) {
                    errors.Add("Unity version is required in the configuration file");
                }
                else {
                    if (string.IsNullOrEmpty(TomlConfig.Global.UnityEditorPath)) {
                        errors.Add("Unity executable not found. Please specify unity version or path.");
                    }
                }

                if (string.IsNullOrEmpty(TomlConfig.Global.UnityHubPath)) {
                    errors.Add("Unity hub path is required in the configuration file");
                }

                var availableBundles = TomlConfig.Bundles.Keys.ToList();
                foreach (string bundleName in BundleConfigNames) {
                    if (!TomlConfig.Bundles.ContainsKey(bundleName)) {
                        string availableBundlesStr = string.Join(", ", availableBundles);
                        errors.Add(
                            $"Bundle configuration '{bundleName}' not found in {Path.GetFileName(ConfigFile)}. Available bundles: {availableBundlesStr}");
                    }
                }
            }
            catch (Exception ex) {
                errors.Add($"Error reading configuration file: {ex.Message}");
            }
        }

        // Validate build targets
        foreach (string target in BuildTargetList) {
            if (!string.IsNullOrEmpty(target) && !ValidTargets.Contains(target)) {
                errors.Add($"Invalid build target: \"{target}\". Valid values are: windows, mac, linux");
            }
        }

        return errors;
    }

    /// <summary>
    ///     Serialize configuration to JSON format matching TOML structure
    /// </summary>
    public string ToJson() {
        return JsonSerializer.Serialize(TomlConfig,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }

    /// <summary>
    ///     Serialize configuration to TOML format using original parsed data
    /// </summary>
    public string ToToml() {
        return TomletMain.TomlStringFrom(TomlConfig);
    }
}