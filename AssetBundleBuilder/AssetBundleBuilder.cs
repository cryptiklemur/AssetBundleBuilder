using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CryptikLemur.AssetBundleBuilder.Config;
using CryptikLemur.AssetBundleBuilder.Interfaces;
using CryptikLemur.AssetBundleBuilder.Utilities;
using Serilog;
using Serilog.Events;
using Tomlet;

namespace CryptikLemur.AssetBundleBuilder;

public static class Program {
    public static Configuration Config { get; set; } = null!;
    public static IProcessRunner ProcessRunner { get; set; } = new SystemProcessRunner();
    public static IFileSystemOperations FileSystem { get; set; } = new SystemFileOperations();

    internal static ILogger Logger { get; set; } = CreateDefaultLogger();

    private static ILogger CreateDefaultLogger() {
        try {
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
                .CreateLogger();
        } catch {
            // Fallback to a minimal logger if console setup fails
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .CreateLogger();
        }
    }

    internal static void InitializeLogging(VerbosityLevel verbosity) {
        try {
            var logLevel = verbosity switch {
                VerbosityLevel.Quiet => LogEventLevel.Error,
                VerbosityLevel.Normal => LogEventLevel.Information,
                VerbosityLevel.Verbose => LogEventLevel.Debug,
                VerbosityLevel.Debug => LogEventLevel.Verbose,
                _ => LogEventLevel.Information
            };

            Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
                .CreateLogger();

            Log.Logger = Logger;
        } catch {
            // If logging initialization fails, keep the existing logger
            // This ensures we never break the application due to logging issues
        }
    }

    private static int Main(string[] args) {
        return CommandLineParser.ParseAndExecuteAsync(args).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Build multiple asset bundles from TOML configuration
    /// </summary>
    public static async Task<int> BuildAssetBundles(Configuration config) {
        // Initialize global config
        Config = config;
        Logger.Information("Starting Unity Asset Builder");

        if (config.TomlConfig.Bundles.Count == 0) {
            Logger.Error("No bundles defined in TOML configuration");
            return 1;
        }

        var bundlesToBuild = new List<UnityBundleConfig>();

        // Determine which bundles to build
        var bundleNamesToProcess = config.BundleConfigNames.Count > 0
            ? config.BundleConfigNames
            : config.TomlConfig.Bundles.Keys.ToList();

        // Determine which targets to build
        /*var targetsToProcess = config.BuildTargetList.Count > 0 && !config.BuildTargetList.Contains("none")
            ? config.BuildTargetList
            : config.TomlConfig.Global.AllowedTargets.ToList();*/

        // Create bundle configurations for each combination of bundle and target
        foreach (string bundleName in bundleNamesToProcess) {
            if (!config.TomlConfig.Bundles.TryGetValue(bundleName, out var bundleConfig)) {
                Logger.Warning("Bundle '{BundleName}' not found in configuration, skipping", bundleName);
                continue;
            }

            var unityConfig = new UnityBundleConfig {
                bundleName = bundleConfig.BundleName,
                bundlePath = string.IsNullOrEmpty(bundleConfig.BundlePath)
                    ? bundleConfig.BundleName
                    : bundleConfig.BundlePath,
                assetDirectory = GetEffectiveAssetDirectory(bundleConfig, config.TomlConfig.Global),
                outputDirectory = GetEffectiveOutputDirectory(bundleConfig, config.TomlConfig.Global),
                buildTargets = null, // null for targetless bundles
                noPlatformSuffix = true,
                filenameFormat = bundleConfig.Filename,
                includePatterns = bundleConfig.IncludePatterns,
                excludePatterns = bundleConfig.ExcludePatterns,
                textureTypes = ConvertTextureTypes(bundleConfig.TextureTypes ?? config.TomlConfig.Global.TextureTypes)
            };

            // For targetless bundles, buildTargets should be null
            if (bundleConfig.Targetless) {
                bundlesToBuild.Add(unityConfig with { buildTargets = null, noPlatformSuffix = true });
                Logger.Verbose("Added to build queue: {BundleName} (targetless)", bundleName);
            }
            else {
                var targetsToProcess = config.BuildTargetList.Count > 0
                    ? config.BuildTargetList
                    : config.TomlConfig.Global.AllowedTargets.ToList();

                bundlesToBuild.Add(unityConfig with { buildTargets = targetsToProcess, noPlatformSuffix = false });
                Logger.Verbose("Added to build queue: {BundleName} for targets: {Targets}", bundleName,
                    string.Join(", ", targetsToProcess));
            }
        }

        Logger.Verbose("Building {Count} bundles across all targets in single Unity process",
            bundlesToBuild.Count);

        // Manage Unity Hub if not in CI mode
        bool wasHubRunning = false;
        if (!config.CiMode) {
            wasHubRunning = IsUnityHubRunning();
            if (!wasHubRunning) {
                Logger.Verbose("Starting Unity Hub...");
                if (!StartUnityHub()) {
                    Logger.Warning("Could not start Unity Hub. Continuing without it");
                }
            }
        }

        try {
            string unityPath = config.TomlConfig.Global.UnityEditorPath;

            // Create temporary Unity project path if not specified
            if (string.IsNullOrEmpty(config.TomlConfig.Global.TempProjectPath)) {
                // Create hash from toml config
                string hash = HashUtility.ComputeHash(TomletMain.TomlStringFrom(config.TomlConfig));
                config.TomlConfig.Global.TempProjectPath =
                    Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");
            }

            Logger.Debug("Temp Project Path: {TempProjectPath}", config.TomlConfig.Global.TempProjectPath);

            // Handle temp project cleanup if requested
            if (config.TomlConfig.Global.CleanTempProject &&
                FileSystem.DirectoryExists(config.TomlConfig.Global.TempProjectPath)) {
                try {
                    FileSystem.DeleteDirectory(config.TomlConfig.Global.TempProjectPath, true);
                    Logger.Verbose("Cleaned up existing temp project: {TempProjectPath}",
                        config.TomlConfig.Global.TempProjectPath);
                } catch (Exception ex) {
                    Logger.Warning("Could not clean up existing temp project: {Message}", ex.Message);
                }
            }

            // Create Unity project with ALL asset directories linked (only need to do this once)
            var uniqueBundles = bundlesToBuild.GroupBy(b => b.bundleName).Select(g => g.First()).ToList();
            CreateUnityProject(config.TomlConfig.Global.TempProjectPath, uniqueBundles,
                config.TomlConfig.Global.LinkMethod);

            int totalSuccess = 0;
            int totalFailed = 0;
            List<string> failedTargets = [];

            // Create single JSON config file with all bundles for all targets
            var allBundlesConfig = new UnityMultiBundleConfig { bundles = bundlesToBuild };
            string configJson =
                JsonSerializer.Serialize(allBundlesConfig, new JsonSerializerOptions { WriteIndented = true });
            string configPath = Path.Combine(config.TomlConfig.Global.TempProjectPath, "bundle-config.json");
            FileSystem.WriteAllText(configPath, configJson);

            Logger.Debug("Created unified bundle configuration file: {ConfigPath}", configPath);

            // Build Unity command line arguments for single process
            var unityArgsList = new List<string> {
                "-batchmode",
                "-nographics",
                "-quit"
            };

            // Use custom logfile if provided
            if (!string.IsNullOrEmpty(config.TomlConfig.Global.LogFile)) {
                unityArgsList.Add("-logfile");
                unityArgsList.Add(config.TomlConfig.Global.LogFile);
                Logger.Verbose("Unity log will be written to: {LogFile}", config.TomlConfig.Global.LogFile);
            }
            else if (!config.Debug) {
                string logPath = Path.Join(config.TomlConfig.Global.TempProjectPath, "unity.log");
                unityArgsList.Add("-logfile");
                unityArgsList.Add(logPath);
                Logger.Verbose("Unity log will be written to: {LogFile}", logPath);
            }

            unityArgsList.AddRange([
                "-projectPath",
                config.TomlConfig.Global.TempProjectPath,
                "-executeMethod",
                "ModAssetBundleBuilder.BuildBundles",
                "-bundleConfigFile",
                configPath
            ]);

            // Execute Unity once for all targets and bundles
            Logger.Verbose("Launching single Unity process to build all bundles across all targets");

            // Create ProcessStartInfo for Unity execution
            var startInfo = new ProcessStartInfo {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            // Handle shell scripts for testing
            if (unityPath.EndsWith(".sh") || unityPath.EndsWith(".bat")) {
                if (unityPath.EndsWith(".sh")) {
                    startInfo.FileName = "bash";
                    startInfo.Arguments =
                        $"\"{unityPath}\" " + string.Join(" ", unityArgsList.Select(arg => $"\"{arg}\""));
                }
                else {
                    startInfo.FileName = unityPath;
                    startInfo.Arguments = string.Join(" ", unityArgsList.Select(arg => $"\"{arg}\""));
                }
            }
            else {
                startInfo.FileName = unityPath;
                startInfo.Arguments = string.Join(" ", unityArgsList.Select(arg => $"\"{arg}\""));
            }

            // Execute Unity process through our abstraction
            var result = await ProcessRunner.RunAsync(startInfo);

            // Log output and errors
            if (!string.IsNullOrEmpty(result.StandardOutput)) {
                foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
                    Logger.Debug("Unity: {Output}", line.Trim());
                }
            }

            if (!string.IsNullOrEmpty(result.StandardError) && config.Debug) {
                foreach (string line in result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
                    Logger.Debug("Unity Error: {Error}", line.Trim());
                }
            }

            if (!result.Success) {
                Logger.Error("Unity build failed with exit code {ExitCode}", result.ExitCode);
                totalFailed = bundlesToBuild.Count;
            }
            else {
                totalSuccess = bundlesToBuild.Count;
            }

            // Final results
            if (totalFailed > 0) {
                Logger.Error("Build completed with failures. Success: {Success}, Failed: {Failed}", totalSuccess,
                    totalFailed);
                Logger.Error("Failed targets: {FailedTargets}", string.Join(", ", failedTargets));
                return 1;
            }
            else {
                Logger.Information("Successfully built all {Count} bundle configurations!",
                    totalSuccess);
                return 0;
            }
        } catch (Exception ex) {
            Logger.Error("Error: {Message}", ex.Message);
            return 1;
        } finally {
            // Clean up Unity Hub if we started it and not in CI mode
            if (!config.CiMode && !wasHubRunning && IsUnityHubRunning()) {
                Logger.Debug("Stopping Unity Hub...");
                StopUnityHub();
            }

            // Clean up temporary project only if explicitly requested
            if (!string.IsNullOrEmpty(config.TomlConfig.Global.TempProjectPath) &&
                FileSystem.DirectoryExists(config.TomlConfig.Global.TempProjectPath)) {
                if (config.TomlConfig.Global.CleanTempProject) {
                    try {
                        FileSystem.DeleteDirectory(config.TomlConfig.Global.TempProjectPath, true);
                        Logger.Information("Cleaned up temporary project: {TempProjectPath}",
                            config.TomlConfig.Global.TempProjectPath);
                    } catch (Exception ex) {
                        Logger.Warning("Could not clean up temporary project: {Message}", ex.Message);
                    }
                }
                else {
                    Logger.Debug("Temporary project preserved at: {TempProjectPath}",
                        config.TomlConfig.Global.TempProjectPath);
                }
            }
        }
    }

    /// <summary>
    ///     Create Unity project with multiple asset directories linked
    /// </summary>
    private static void CreateUnityProject(string projectPath, List<UnityBundleConfig> bundles,
        string linkMethod) {
        Logger.Verbose("Creating Unity project for {Count} bundles at: {ProjectPath}", bundles.Count, projectPath);

        // Create project structure
        FileSystem.CreateDirectory(projectPath);
        FileSystem.CreateDirectory(Path.Combine(projectPath, "Assets"));
        FileSystem.CreateDirectory(Path.Combine(projectPath, "Assets", "Editor"));
        FileSystem.CreateDirectory(Path.Combine(projectPath, "Assets", "Data"));

        // Create the ModAssetBundleBuilder script
        CreateModAssetBundleBuilderScript(Path.Combine(projectPath, "Assets", "Editor"));

        // Create the AssetLabeler script
        CreateAssetLabelerScript(Path.Combine(projectPath, "Assets", "Editor"));

        // Link/copy all asset directories - avoid duplicates by tracking source directories
        var linkedSources = new HashSet<string>();

        foreach (var bundle in bundles) {
            if (string.IsNullOrEmpty(bundle.assetDirectory)) {
                Logger.Warning("Bundle {BundleName} has no asset directory specified, skipping",
                    bundle.GetBundlePathOrName());
                continue;
            }

            if (!FileSystem.DirectoryExists(bundle.assetDirectory)) {
                Logger.Error("Asset directory not found for bundle {BundleName}: {AssetDirectory}",
                    bundle.GetBundlePathOrName(),
                    bundle.assetDirectory);
                throw new DirectoryNotFoundException($"Asset directory not found: {bundle.assetDirectory}");
            }

            // Use the already-normalized source directory path to avoid duplicates
            string normalizedSource = bundle.assetDirectory;

            if (linkedSources.Contains(normalizedSource)) {
                Logger.Debug(
                    "Skipping asset linking for bundle {BundleName} - source directory already linked: {AssetDirectory}",
                    bundle.GetBundlePathOrName(), bundle.assetDirectory);
                continue;
            }

            // Target directory MUST be named after the bundle
            string targetPath = Path.Combine(projectPath, "Assets", "Data", bundle.GetBundlePathOrName());

            LinkAssets(bundle.assetDirectory, targetPath, linkMethod);
            linkedSources.Add(normalizedSource);

            Logger.Debug("Linked assets for bundle {BundleName}: {AssetDirectory} -> {TargetPath}",
                bundle.bundleName, bundle.assetDirectory, targetPath);
        }

        Logger.Debug("Unity project setup complete");
    }

    // Include the embedded C# scripts here...
    private static void CreateModAssetBundleBuilderScript(string editorPath) {
        string sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnityScripts",
            "ModAssetBundleBuilder.cs");
        string targetFile = Path.Combine(editorPath, "ModAssetBundleBuilder.cs");
        FileSystem.CopyFile(sourceFile, targetFile, true);
    }

    private static void CreateAssetLabelerScript(string editorPath) {
        string sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnityScripts", "AssetLabeler.cs");
        string targetFile = Path.Combine(editorPath, "AssetLabeler.cs");
        FileSystem.CopyFile(sourceFile, targetFile, true);
    }

    // Unity scripts are now copied from UnityScripts/ directory instead of being embedded

    private static void LinkAssets(string sourceDirectory, string targetPath, string linkMethod) {
        Logger.Debug("Linking assets using method: {LinkMethod}", linkMethod);
        Logger.Debug("Source: {SourceDirectory}", sourceDirectory);
        Logger.Debug("Target: {TargetPath}", targetPath);

        // Ensure target directory doesn't exist
        if (FileSystem.DirectoryExists(targetPath)) {
            FileSystem.DeleteDirectory(targetPath, true);
        }

        // Ensure parent directory exists
        string parentDir = Path.GetDirectoryName(targetPath)!;
        if (!FileSystem.DirectoryExists(parentDir)) {
            FileSystem.CreateDirectory(parentDir);
        }

        switch (linkMethod.ToLower()) {
            case "copy":
                CopyDirectory(sourceDirectory, targetPath);
                Logger.Verbose("Assets copied successfully");
                break;
            case "symlink":
                FileSystem.CreateSymbolicLink(targetPath, sourceDirectory);
                Logger.Verbose("Symbolic link created successfully");
                break;
            case "hardlink":
                CreateHardLink(sourceDirectory, targetPath);
                Logger.Verbose("Hard links created successfully");
                break;
            case "junction":
                FileSystem.CreateJunction(targetPath, sourceDirectory);
                Logger.Verbose("Junction created successfully");
                break;
            default:
                throw new ArgumentException($"Unknown link method: {linkMethod}");
        }
    }

    // CopyDirectory helper method
    private static void CopyDirectory(string sourceDir, string destDir) {
        FileSystem.CreateDirectory(destDir);

        foreach (string dirPath in FileSystem.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)) {
            FileSystem.CreateDirectory(dirPath.Replace(sourceDir, destDir));
        }

        foreach (string filePath in FileSystem.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)) {
            string destPath = filePath.Replace(sourceDir, destDir);
            string? destDirPath = Path.GetDirectoryName(destPath);
            if (destDirPath != null && !FileSystem.DirectoryExists(destDirPath)) {
                FileSystem.CreateDirectory(destDirPath);
            }

            FileSystem.CopyFile(filePath, destPath, true);
        }
    }

    private static void CreateHardLink(string sourceDirectory, string targetPath) {
        // Hard links work differently - we need to recursively create hard links for files
        FileSystem.CreateDirectory(targetPath);

        foreach (string dirPath in FileSystem.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories)) {
            string relativePath = Path.GetRelativePath(sourceDirectory, dirPath);
            string targetDirPath = Path.Combine(targetPath, relativePath);
            FileSystem.CreateDirectory(targetDirPath);
        }

        foreach (string filePath in FileSystem.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories)) {
            string relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            string targetFilePath = Path.Combine(targetPath, relativePath);

            try {
                FileSystem.CreateHardLink(targetFilePath, filePath);
            } catch (Exception ex) {
                throw new InvalidOperationException(
                    $"Failed to create hard link for {filePath}. {ex.Message}", ex);
            }
        }
    }

    internal static int RunCommand(string command, string arguments) {
        ProcessStartInfo processInfo;

        // mklink is a built-in Windows command, must be run through cmd.exe
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && command == "mklink") {
            processInfo = new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/C {command} {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }
        else {
            processInfo = new ProcessStartInfo {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        using var process = Process.Start(processInfo);
        if (process == null) {
            throw new InvalidOperationException($"Failed to start process: {command}");
        }

        process.WaitForExit();
        return process.ExitCode;
    }


    private static bool IsUnityHubRunning() {
        try {
            var processes = Process.GetProcessesByName("Unity Hub");
            return processes.Length > 0;
        } catch {
            return false;
        }
    }

    private static bool StartUnityHub() {
        try {
            var processInfo = new ProcessStartInfo {
                FileName = Config.TomlConfig.Global.UnityHubPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(processInfo);
            if (process == null) {
                return false;
            }

            // Consume Unity Hub output to prevent it from appearing in console
            process.OutputDataReceived += (_, _) => {
                /* consume and discard */
            };
            process.ErrorDataReceived += (_, _) => {
                /* consume and discard */
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Give Unity Hub a moment to start up
            Thread.Sleep(2000);
            return IsUnityHubRunning();
        } catch {
            return false;
        }
    }

    private static void StopUnityHub() {
        try {
            var processes = Process.GetProcessesByName("Unity Hub");
            foreach (var process in processes) {
                try {
                    process.Kill();
                    process.WaitForExit(5000); // Wait up to 5 seconds
                } catch {
                    // Ignore errors when killing processes
                }
            }
        } catch {
            // Ignore errors
        }
    }

    private static string GetEffectiveAssetDirectory(TomlBundleConfig bundleConfig, TomlGlobalConfig globalConfig) {
        string assetDirectory = string.IsNullOrEmpty(bundleConfig.AssetDirectory)
            ? globalConfig.AssetDirectory
            : bundleConfig.AssetDirectory;

        if (string.IsNullOrEmpty(assetDirectory)) {
            throw new ArgumentException("Asset directory is required in either bundle config or global config");
        }

        return Path.IsPathRooted(assetDirectory)
            ? assetDirectory
            : Path.GetFullPath(assetDirectory);
    }

    private static string GetEffectiveOutputDirectory(TomlBundleConfig bundleConfig, TomlGlobalConfig globalConfig) {
        string outputDirectory = string.IsNullOrEmpty(bundleConfig.OutputDirectory)
            ? globalConfig.OutputDirectory
            : bundleConfig.OutputDirectory;

        if (string.IsNullOrEmpty(outputDirectory)) {
            outputDirectory = Directory.GetCurrentDirectory();
        }

        return Path.IsPathRooted(outputDirectory)
            ? outputDirectory
            : Path.GetFullPath(outputDirectory);
    }

    private static Dictionary<string, UnityTextureTypeConfig> ConvertTextureTypes(Dictionary<string, Config.TextureTypeConfig> tomlTypes) {
        var result = new Dictionary<string, UnityTextureTypeConfig>();
        foreach (var kvp in tomlTypes) {
            result[kvp.Key] = new UnityTextureTypeConfig { patterns = kvp.Value.Patterns };
        }
        return result;
    }

    /// <summary>
    ///     Bundle configuration for Unity multi-bundle building
    /// </summary>
    public record UnityBundleConfig {
        public string bundleName { get; set; } = "";
        public string bundlePath { get; set; } = "";
        public string assetDirectory { get; set; } = "";
        public string outputDirectory { get; set; } = "";
        public List<string>? buildTargets { get; set; } // null for targetless bundles, array for targeted bundles
        public bool noPlatformSuffix { get; set; }
        public string filenameFormat { get; set; } = "";
        public List<string> includePatterns { get; set; } = [];
        public List<string> excludePatterns { get; set; } = [];
        public Dictionary<string, UnityTextureTypeConfig> textureTypes { get; set; } = [];

        public string GetBundlePathOrName() {
            return string.IsNullOrEmpty(bundlePath) ? bundleName : bundlePath;
        }
    }

    public class UnityTextureTypeConfig {
        public List<string> patterns { get; set; } = [];
    }

    /// <summary>
    ///     Container for multiple bundle configurations
    /// </summary>
    public class UnityMultiBundleConfig {
        public List<UnityBundleConfig> bundles { get; set; } = [];
    }
}