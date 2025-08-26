using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CryptikLemur.AssetBundleBuilder;

public static class Program {
    private static int Main(string[] args) {
        // Handle special commands
        if (args.Length == 2 && args[0] == "--list-bundles") return ListBundles(args[1]);

        if (args.Length < 2) {
            ShowHelp();
            return args.Length == 0 ? 0 : 1;
        }

        var cliConfig = ArgumentParser.Parse(args);
        if (cliConfig == null) {
            Console.WriteLine("Error: Invalid arguments provided.");
            ShowHelp();
            return 1;
        }

        // Handle configuration file loading
        BuildConfiguration config;
        if (!string.IsNullOrEmpty(cliConfig.ConfigFile)) {
            try {
                config = ConfigurationLoader.LoadFromToml(cliConfig.ConfigFile, cliConfig.BundleConfigName, cliConfig);
            }
            catch (Exception ex) {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                return 1;
            }
        }
        else config = cliConfig;

        // Initialize global config and logging
        GlobalConfig.Config = config;
        GlobalConfig.InitializeLogging(config.Verbosity);

        return BuildAssetBundle(config);
    }

    private static int ListBundles(string configPath) {
        try {
            var bundles = ConfigurationLoader.ListBundles(configPath);
            if (bundles.Count == 0) {
                Console.WriteLine("No bundles found in configuration file.");
                return 1;
            }

            var output = new StringBuilder();
            output.AppendLine($"Available bundles in {Path.GetFileName(configPath)}:");
            foreach (var bundle in bundles) output.AppendLine($"  - {bundle}");
            Console.Write(output.ToString());
            return 0;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error reading configuration: {ex.Message}");
            return 1;
        }
    }

    public static int BuildAssetBundle(BuildConfiguration config) {
        // If unity version is specified, try to find the Unity executable
        if (!string.IsNullOrEmpty(config.UnityVersion) && string.IsNullOrEmpty(config.UnityPath)) {
            config.UnityPath = UnityPathFinder.FindUnityExecutable(config.UnityVersion) ?? "";
            if (string.IsNullOrEmpty(config.UnityPath)) {
                if (config.CiMode) {
                    GlobalConfig.Logger.Error("Could not find Unity {UnityVersion} installation", config.UnityVersion);
                    GlobalConfig.Logger.Error("In CI mode, Unity must be pre-installed. Auto-installation is disabled");
                    return 1;
                }

                var installResult = PromptAndInstallUnity(config.UnityVersion, config.NonInteractive);
                if (installResult == 0) {
                    // Retry finding Unity after installation
                    config.UnityPath = UnityPathFinder.FindUnityExecutable(config.UnityVersion) ?? "";
                }

                if (string.IsNullOrEmpty(config.UnityPath)) {
                    GlobalConfig.Logger.Error(
                        "Could not find Unity {UnityVersion} installation even after attempted installation",
                        config.UnityVersion);
                    return 1;
                }
            }

            GlobalConfig.Logger.Information("Found Unity {UnityVersion} at: {UnityPath}", config.UnityVersion,
                config.UnityPath);
        }

        // Validate paths
        if (string.IsNullOrEmpty(config.UnityPath)) {
            GlobalConfig.Logger.Error("Unity path not specified and could not be auto-detected");
            return 1;
        }

        if (!File.Exists(config.UnityPath)) {
            GlobalConfig.Logger.Error("Unity executable not found at '{UnityPath}'", config.UnityPath);
            return 1;
        }

        if (!Directory.Exists(config.AssetDirectory)) {
            GlobalConfig.Logger.Error("Asset directory not found at '{AssetDirectory}'", config.AssetDirectory);
            return 1;
        }

        GlobalConfig.Logger.Information("Using bundle name: {BundleName}", config.BundleName);

        // Determine if we're using auto-detected target (no platform suffix) or explicit target
        var isAutoTarget = string.IsNullOrEmpty(config.BuildTarget);

        // If no build target specified, detect current OS and don't append platform suffix
        if (isAutoTarget) {
            config.BuildTarget = DetectCurrentOS();
            GlobalConfig.Logger.Information("No target specified, using current OS: {BuildTarget} (no platform suffix)",
                config.BuildTarget);
        }
        else GlobalConfig.Logger.Information("Using specified build target: {BuildTarget}", config.BuildTarget);

        // Convert user-friendly build target to Unity command line format
        var unityBuildTarget = ConvertBuildTarget(config.BuildTarget);
        GlobalConfig.Logger.Information("Unity build target: {BuildTarget}", unityBuildTarget);

        // Create temporary Unity project if not specified
        if (string.IsNullOrEmpty(config.TempProjectPath)) {
            // Create hash from input parameters for consistent temp directory naming
            // Include a flag to distinguish between explicit and auto-detected targets
            var targetForHash = isAutoTarget ? "auto" : config.BuildTarget;
            var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{targetForHash}";
            var hash = HashUtility.ComputeHash(hashInput);
            config.TempProjectPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");
        }

        // Handle temp project cleanup if requested
        if (config.CleanTempProject && Directory.Exists(config.TempProjectPath)) {
            try {
                Directory.Delete(config.TempProjectPath, true);
                GlobalConfig.Logger.Information("Cleaned up existing temp project: {TempProjectPath}",
                    config.TempProjectPath);
            }
            catch (Exception ex) {
                GlobalConfig.Logger.Warning("Could not clean up existing temp project: {Message}", ex.Message);
            }
        }

        GlobalConfig.Logger.Information("Starting Unity Asset Bundle Builder");
        GlobalConfig.Logger.Debug("Unity Path: {UnityPath}", config.UnityPath);
        GlobalConfig.Logger.Debug("Temp Project Path: {TempProjectPath}", config.TempProjectPath);
        GlobalConfig.Logger.Debug("Asset Directory: {AssetDirectory}", config.AssetDirectory);
        GlobalConfig.Logger.Debug("Bundle Name: {BundleName}", config.BundleName);
        GlobalConfig.Logger.Debug("Output Directory: {OutputDirectory}", config.OutputDirectory);
        GlobalConfig.Logger.Debug("Build Target: {BuildTarget}", config.BuildTarget);

        // Manage Unity Hub if not in CI mode
        var wasHubRunning = false;
        if (!config.CiMode) {
            wasHubRunning = IsUnityHubRunning();
            if (!wasHubRunning) {
                GlobalConfig.Logger.Information("Starting Unity Hub...");
                if (!StartUnityHub())
                    GlobalConfig.Logger.Warning("Could not start Unity Hub. Continuing without it");
            }
        }

        try {
            // Ensure output directory exists
            if (!Directory.Exists(config.OutputDirectory)) {
                Directory.CreateDirectory(config.OutputDirectory);
                GlobalConfig.Logger.Information("Created output directory: {OutputDirectory}", config.OutputDirectory);
            }

            // Create temporary Unity project
            CreateUnityProject(config.TempProjectPath, config.AssetDirectory, config.BundleName, config.LinkMethod);

            // Build Unity command line arguments
            var unityArgsList = new List<string> {
                "-batchmode",
                "-nographics",
                "-quit"
            };

            // Use custom logfile if provided
            if (!string.IsNullOrEmpty(config.LogFile)) {
                unityArgsList.Add("-logfile");
                unityArgsList.Add(config.LogFile);
                GlobalConfig.Logger.Information("Unity log will be written to: {LogFile}", config.LogFile);
            }

            unityArgsList.AddRange([
                "-projectPath",
                config.TempProjectPath,
                "-executeMethod",
                "ModAssetBundleBuilder.BuildBundles",
                "-buildTarget",
                unityBuildTarget,
                "-bundleName",
                config.BundleName,
                "-output",
                config.OutputDirectory,
                "-assetDirectory",
                config.AssetDirectory,
                "-noPlatformSuffix",
                isAutoTarget ? "true" : "false"
            ]);

            // Add include patterns if any
            if (config.IncludePatterns.Count > 0) {
                unityArgsList.Add("-includePatterns");
                unityArgsList.Add(string.Join(";", config.IncludePatterns));
            }

            // Add exclude patterns if any
            if (config.ExcludePatterns.Count > 0) {
                unityArgsList.Add("-excludePatterns");
                unityArgsList.Add(string.Join(";", config.ExcludePatterns));
            }

            var unityArgs = unityArgsList.ToArray();

            var processInfo = new ProcessStartInfo {
                FileName = config.UnityPath,
                Arguments = string.Join(" ", unityArgs.Select(arg => arg.Contains(" ") ? $"\"{arg}\"" : arg)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            GlobalConfig.Logger.Information("Launching Unity...");
            using (var process = Process.Start(processInfo)) {
                if (process == null) throw new Exception("Launching Unity failed.");

                // Read Unity output based on verbosity level
                process.OutputDataReceived += (_, e) => {
                    if (!string.IsNullOrEmpty(e.Data) && !e.Data.TrimStart().StartsWith("[Experiment"))
                        GlobalConfig.Logger.Debug("Unity: {Output}", e.Data);
                };
                process.ErrorDataReceived += (_, e) => {
                    if (!string.IsNullOrEmpty(e.Data) && !e.Data.TrimStart().StartsWith("[Experiment"))
                        GlobalConfig.Logger.Debug("Unity Error: {Output}", e.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                if (process.ExitCode != 0) {
                    GlobalConfig.Logger.Error("Unity failed with exit code {ExitCode}", process.ExitCode);
                    return process.ExitCode;
                }
            }

            GlobalConfig.Logger.Information("Asset bundles built successfully!");
            return 0;
        }
        catch (Exception ex) {
            GlobalConfig.Logger.Error("Error: {Message}", ex.Message);
            return 1;
        }
        finally {
            // Clean up Unity Hub if we started it and not in CI mode
            if (!config.CiMode && !wasHubRunning && IsUnityHubRunning()) {
                GlobalConfig.Logger.Information("Stopping Unity Hub...");
                StopUnityHub();
            }

            // Clean up temporary project only if explicitly requested
            if (config.CleanTempProject && Directory.Exists(config.TempProjectPath)) {
                try {
                    Directory.Delete(config.TempProjectPath, true);
                    GlobalConfig.Logger.Information("Cleaned up temporary project: {TempProjectPath}",
                        config.TempProjectPath);
                }
                catch (Exception ex) {
                    GlobalConfig.Logger.Warning("Could not clean up temporary project: {Message}", ex.Message);
                }
            }
            else if (!config.CleanTempProject && Directory.Exists(config.TempProjectPath))
                GlobalConfig.Logger.Debug("Temporary project preserved at: {TempProjectPath}", config.TempProjectPath);
        }
    }

    private static void ShowHelp() {
        const string help = """
                            AssetBundleBuilder - Unity Asset Bundle Builder for RimWorld Mods

                            Usage: AssetBundleBuilder [unity-path-or-version] <asset-directory> <output-directory> <bundle-name> [options]
                               or: AssetBundleBuilder --unity-version <version> <asset-directory> <output-directory> <bundle-name> [options]

                            Arguments:
                              unity-path-or-version  Either:
                                                     - Path to Unity executable (e.g., C:\Unity\Editor\Unity.exe)
                                                     - Unity version (e.g., 2022.3.35f1) - will auto-find executable
                              asset-directory        Path to the directory containing assets to bundle
                              output-directory       Path where the asset bundles will be created
                              bundle-name            Name for the asset bundle (e.g., mymod, author.modname)

                            Options:
                              --unity-version <ver>  Specify Unity version explicitly (e.g., 2022.3.35f1)
                              --bundle-name <name>   Override bundle name (alternative to positional argument)
                              --target <target>      Build target: windows, mac, or linux (default: windows)
                              --temp-project <path>  Custom path for temporary Unity project
                              --clean-temp           Delete temporary project after build (default: keep for caching)
                              --copy                 Copy assets to Unity project (default)
                              --symlink              Create symbolic link to assets (faster builds)
                              --hardlink             Create hard links to assets (Windows/Unix)
                              --junction             Create directory junction to assets (Windows only)
                              --logfile <path>       Write Unity log to specified file (default: stdout)
                              --ci                   Run in CI mode (no interactive prompts, no Unity auto-install)
                              -q, --quiet            Show only errors
                              -v, --verbose          Show info, warnings, and errors (default)
                              -vv, --debug           Show all messages including debug output
                              --non-interactive      Auto-answer yes to prompts, exit on errors
                              --exclude <pattern>    Exclude files matching glob pattern (can be used multiple times)
                              --include <pattern>    Include only files matching glob pattern (can be used multiple times)
                              --config <path>        Use TOML configuration file
                              --bundle-config <name> Select specific bundle from config file
                              --list-bundles <path>  List available bundles in config file
                              
                            Auto-detection:
                              .assetbundler.toml     Automatically detected if present in current directory

                            Examples:
                              # CLI usage:
                              AssetBundleBuilder 2022.3.35f1 "C:\MyMod\Assets" "C:\MyMod\Output" "mymod"
                              AssetBundleBuilder 2022.3.35f1 ./assets ./output mymod --exclude "*.tmp"
                              AssetBundleBuilder 2022.3.35f1 ./assets ./output mymod --include "*.png" --include "*.jpg"

                              # Config file usage:
                              AssetBundleBuilder --config mymod.toml
                              AssetBundleBuilder --config mymod.toml --bundle-config sounds
                              AssetBundleBuilder --list-bundles mymod.toml

                              # Auto-detection (.assetbundler.toml in current directory):
                              AssetBundleBuilder --bundle-config textures
                              AssetBundleBuilder -vv --bundle-config sounds
                              AssetBundleBuilder --target mac --verbose

                              # Config file can override CLI args:
                              AssetBundleBuilder --config mymod.toml --target mac --verbose

                            Example config file (mymod.toml):
                              [global]
                              unity_version = "2022.3.35f1"
                              build_target = "windows"
                              exclude_patterns = ["*.tmp", "backup/*"]

                              [bundles.textures]
                              asset_directory = "Assets/Textures"
                              bundle_name = "author.textures"
                              include_patterns = ["*.png", "*.jpg"]

                              [bundles.sounds]
                              asset_directory = "Assets/Sounds"
                              bundle_name = "author.sounds"
                              include_patterns = ["*.wav", "*.ogg"]
                            """;

        Console.WriteLine(help);
    }

    private static void CreateUnityProject(string projectPath, string assetDirectory, string bundleName,
        string linkMethod) {
        GlobalConfig.Logger.Information("Creating temporary Unity project at: {ProjectPath}", projectPath);

        // Create project structure
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets", "Editor"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets", "Data"));
        // Unity will create ProjectSettings automatically when it opens the project

        // Create the ModAssetBundleBuilder script
        CreateModAssetBundleBuilderScript(Path.Combine(projectPath, "Assets", "Editor"));

        // Create the AssetLabeler script
        CreateAssetLabelerScript(Path.Combine(projectPath, "Assets", "Editor"));

        // Create the PSDMatteUtility script
        CreatePSDMatteUtilityScript(Path.Combine(projectPath, "Assets", "Editor"));

        // Link assets from source directory to Unity project
        var targetPath = Path.Combine(projectPath, "Assets", "Data", bundleName);
        LinkAssets(assetDirectory, targetPath, linkMethod);

        GlobalConfig.Logger.Information("Unity project created successfully");
    }

    // Include the embedded C# scripts here...
    private static void CreateModAssetBundleBuilderScript(string editorPath) {
        var sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnityScripts",
            "ModAssetBundleBuilder.cs");
        var targetFile = Path.Combine(editorPath, "ModAssetBundleBuilder.cs");
        File.Copy(sourceFile, targetFile, true);
    }

    private static void CreateAssetLabelerScript(string editorPath) {
        var sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnityScripts", "AssetLabeler.cs");
        var targetFile = Path.Combine(editorPath, "AssetLabeler.cs");
        File.Copy(sourceFile, targetFile, true);
    }

    // ReSharper disable once InconsistentNaming
    private static void CreatePSDMatteUtilityScript(string editorPath) {
        var sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnityScripts", "PSDMatteUtility.cs");
        var targetFile = Path.Combine(editorPath, "PSDMatteUtility.cs");
        File.Copy(sourceFile, targetFile, true);
    }

    // Unity scripts are now copied from UnityScripts/ directory instead of being embedded

    private static void LinkAssets(string sourceDirectory, string targetPath, string linkMethod) {
        GlobalConfig.Logger.Information("Linking assets using method: {LinkMethod}", linkMethod);
        GlobalConfig.Logger.Debug("Source: {SourceDirectory}", sourceDirectory);
        GlobalConfig.Logger.Debug("Target: {TargetPath}", targetPath);

        // Ensure target directory doesn't exist
        if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);

        // Ensure parent directory exists
        var parentDir = Path.GetDirectoryName(targetPath)!;
        if (!Directory.Exists(parentDir)) Directory.CreateDirectory(parentDir);

        switch (linkMethod.ToLower()) {
            case "copy":
                CopyDirectory(sourceDirectory, targetPath);
                GlobalConfig.Logger.Information("Assets copied successfully");
                break;
            case "symlink":
                CreateSymbolicLink(sourceDirectory, targetPath);
                GlobalConfig.Logger.Information("Symbolic link created successfully");
                break;
            case "hardlink":
                CreateHardLink(sourceDirectory, targetPath);
                GlobalConfig.Logger.Information("Hard links created successfully");
                break;
            case "junction":
                CreateJunction(sourceDirectory, targetPath);
                GlobalConfig.Logger.Information("Junction created successfully");
                break;
            default:
                throw new ArgumentException($"Unknown link method: {linkMethod}");
        }
    }

    // CopyDirectory helper method
    private static void CopyDirectory(string sourceDir, string destDir) {
        Directory.CreateDirectory(destDir);

        foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)) {
            Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));
        }

        foreach (var filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)) {
            var destPath = filePath.Replace(sourceDir, destDir);
            var destDirPath = Path.GetDirectoryName(destPath);
            if (destDirPath != null && !Directory.Exists(destDirPath))
                Directory.CreateDirectory(destDirPath);
            File.Copy(filePath, destPath, true);
        }
    }

    private static void CreateSymbolicLink(string sourceDirectory, string targetPath) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var result = RunCommand("mklink", $"/D \"{targetPath}\" \"{sourceDirectory}\"");
            if (result != 0)
                throw new InvalidOperationException($"Failed to create symbolic link. Exit code: {result}");
        }
        else {
            var result = RunCommand("ln", $"-s \"{sourceDirectory}\" \"{targetPath}\"");
            if (result != 0)
                throw new InvalidOperationException($"Failed to create symbolic link. Exit code: {result}");
        }
    }

    private static void CreateHardLink(string sourceDirectory, string targetPath) {
        // Hard links work differently - we need to recursively create hard links for files
        Directory.CreateDirectory(targetPath);

        foreach (var dirPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories)) {
            var relativePath = Path.GetRelativePath(sourceDirectory, dirPath);
            var targetDirPath = Path.Combine(targetPath, relativePath);
            Directory.CreateDirectory(targetDirPath);
        }

        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories)) {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var targetFilePath = Path.Combine(targetPath, relativePath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var result = RunCommand("mklink", $"/H \"{targetFilePath}\" \"{filePath}\"");
                if (result != 0) {
                    throw new InvalidOperationException(
                        $"Failed to create hard link for {filePath}. Exit code: {result}");
                }
            }
            else {
                var result = RunCommand("ln", $"\"{filePath}\" \"{targetFilePath}\"");
                if (result != 0) {
                    throw new InvalidOperationException(
                        $"Failed to create hard link for {filePath}. Exit code: {result}");
                }
            }
        }
    }

    private static void CreateJunction(string sourceDirectory, string targetPath) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Junctions are only supported on Windows.");

        var result = RunCommand("mklink", $"/J \"{targetPath}\" \"{sourceDirectory}\"");
        if (result != 0)
            throw new InvalidOperationException($"Failed to create junction. Exit code: {result}");
    }


    private static int RunCommand(string command, string arguments) {
        ProcessStartInfo processInfo;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && command == "mklink")
            // mklink is a built-in Windows command, must be run through cmd.exe
        {
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
        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {command}");

        process.WaitForExit();
        return process.ExitCode;
    }

    private static string DetectCurrentOS() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "mac";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";

        throw new PlatformNotSupportedException("Unsupported operating system");
    }

    private static string ConvertBuildTarget(string userBuildTarget) {
        return userBuildTarget.ToLower() switch {
            "windows" => "StandaloneWindows64",
            "mac" => "StandaloneOSX",
            "linux" => "StandaloneLinux64",
            _ => throw new ArgumentException($"Unsupported build target: {userBuildTarget}")
        };
    }

    private static int PromptAndInstallUnity(string version, bool nonInteractive = false) {
        GlobalConfig.Logger.Warning("Unity {Version} was not found on this system", version);

        if (nonInteractive) {
            GlobalConfig.Logger.Information(
                "Non-interactive mode: Automatically installing Unity Hub and Unity Editor...");
        }
        else {
            Console.WriteLine("Would you like to automatically download and install Unity Hub and Unity Editor? (Y/n)");
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response is "n" or "no") {
                GlobalConfig.Logger.Information("Installation cancelled by user");
                return 1;
            }
        }

        try {
            // Step 1: Install Unity Hub if not present
            if (!IsUnityHubInstalled()) {
                GlobalConfig.Logger.Information("Installing Unity Hub...");
                var hubResult = InstallUnityHub(nonInteractive);
                if (hubResult != 0) {
                    GlobalConfig.Logger.Error("Failed to install Unity Hub");
                    if (nonInteractive) {
                        GlobalConfig.Logger.Error(
                            "Non-interactive mode: Exiting due to Unity Hub installation failure");
                        return hubResult;
                    }

                    return hubResult;
                }

                GlobalConfig.Logger.Information("Unity Hub installed successfully");
            }
            else GlobalConfig.Logger.Information("Unity Hub is already installed");

            // Step 2: Install Unity Editor version via Hub
            GlobalConfig.Logger.Information("Installing Unity Editor {Version}...", version);
            var editorResult = InstallUnityEditor(version, nonInteractive);
            if (editorResult != 0) {
                GlobalConfig.Logger.Error("Failed to install Unity Editor {Version}", version);
                if (nonInteractive) {
                    GlobalConfig.Logger.Error("Non-interactive mode: Exiting due to Unity Editor installation failure");
                    return editorResult;
                }

                return editorResult;
            }

            GlobalConfig.Logger.Information("Unity Editor {Version} installed successfully", version);
            return 0;
        }
        catch (Exception ex) {
            GlobalConfig.Logger.Error("Error during Unity installation: {Message}", ex.Message);
            return 1;
        }
    }

    private static bool IsUnityHubInstalled() {
        return GetUnityHubExecutablePath() != null;
    }

    private static string? GetUnityHubExecutablePath() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var hubPaths = new[] {
                @"C:\Program Files\Unity Hub\Unity Hub.exe",
                @"C:\Program Files (x86)\Unity Hub\Unity Hub.exe"
            };
            return hubPaths.FirstOrDefault(File.Exists);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            var hubPath = "/Applications/Unity Hub.app/Contents/MacOS/Unity Hub";
            return File.Exists(hubPath) ? hubPath : null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            // Check common Linux installation paths
            var hubPaths = new[] {
                "/opt/unityhub/unityhub",
                "/usr/bin/unityhub"
            };
            return hubPaths.FirstOrDefault(File.Exists);
        }

        return null;
    }

    private static bool IsUnityHubRunning() {
        try {
            var processes = Process.GetProcessesByName("Unity Hub");
            return processes.Length > 0;
        }
        catch {
            return false;
        }
    }

    private static bool StartUnityHub() {
        try {
            var hubPath = GetUnityHubExecutablePath();
            if (string.IsNullOrEmpty(hubPath)) return false;

            var processInfo = new ProcessStartInfo {
                FileName = hubPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(processInfo);
            if (process == null) return false;

            // Consume Unity Hub output to prevent it from appearing in console
            process.OutputDataReceived += (_, e) => {
                /* consume and discard */
            };
            process.ErrorDataReceived += (_, e) => {
                /* consume and discard */
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Give Unity Hub a moment to start up
            Thread.Sleep(2000);
            return IsUnityHubRunning();
        }
        catch {
            return false;
        }
    }

    private static void StopUnityHub() {
        try {
            var processes = Process.GetProcessesByName("Unity Hub");
            foreach (var process in processes)
                try {
                    process.Kill();
                    process.WaitForExit(5000); // Wait up to 5 seconds
                }
                catch {
                    // Ignore errors when killing processes
                }
        }
        catch {
            // Ignore errors
        }
    }

    private static string? GetUnityChangeset(string version) {
        try {
            var url = $"https://services.api.unity.com/unity/editor/release/v1/releases?version={version}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AssetBundleBuilder");

            var response = client.GetAsync(url).Result;

            if (!response.IsSuccessStatusCode) return null;

            var responseContent = response.Content.ReadAsStringAsync().Result;

            // Parse JSON to extract changeset from shortRevision
            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0) {
                var firstResult = results[0];
                if (firstResult.TryGetProperty("shortRevision", out var shortRevision)) {
                    var changesetValue = shortRevision.GetString();
                    if (!string.IsNullOrEmpty(changesetValue)) {
                        GlobalConfig.Logger.Debug("Found changeset: {Changeset}", changesetValue);
                        return changesetValue;
                    }
                }
            }

            GlobalConfig.Logger.Debug("No changeset found in API response");
            return null;
        }
        catch (Exception ex) {
            GlobalConfig.Logger.Warning("Error querying Unity changeset via REST API: {Message}", ex.Message);
            return null;
        }
    }

    private static int InstallUnityHub(bool nonInteractive = false) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return InstallUnityHubWindows();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return InstallUnityHubMac();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return InstallUnityHubLinux();

        GlobalConfig.Logger.Error("Unsupported platform for automatic Unity Hub installation");
        return 1;
    }

    private static int InstallUnityHubWindows() {
        var hubUrl = "https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe";
        var tempFile = Path.Combine(Path.GetTempPath(), "UnityHubSetup.exe");

        try {
            // Download Unity Hub installer
            GlobalConfig.Logger.Information("Downloading Unity Hub installer...");
            using (var client = new HttpClient()) {
                var response = client.GetAsync(hubUrl).Result;
                response.EnsureSuccessStatusCode();

                using (var fileStream = File.Create(tempFile)) {
                    response.Content.CopyToAsync(fileStream).Wait();
                }
            }

            // Run the installer silently
            GlobalConfig.Logger.Information("Running Unity Hub installer...");
            var processInfo = new ProcessStartInfo {
                FileName = tempFile,
                Arguments = "/S", // Silent installation
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo)) {
                if (process == null) {
                    GlobalConfig.Logger.Error("Failed to start Unity Hub installer");
                    return 1;
                }

                process.WaitForExit();

                if (process.ExitCode != 0) {
                    GlobalConfig.Logger.Error("Unity Hub installer failed with exit code: {ExitCode}",
                        process.ExitCode);
                    return process.ExitCode;
                }
            }

            return 0;
        }
        catch (Exception ex) {
            GlobalConfig.Logger.Error("Error installing Unity Hub: {Message}", ex.Message);
            return 1;
        }
        finally {
            // Clean up temp file
            try {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch {
                // Ignore cleanup errors
            }
        }
    }

    private static int InstallUnityHubMac() {
        var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "darwin-arm64" : "darwin-x64";
        var hubUrl = $"https://public-cdn.cloud.unity3d.com/hub/prod/UnityHub-{architecture}.dmg";
        var tempFile = Path.Combine(Path.GetTempPath(), "UnityHub.dmg");

        try {
            GlobalConfig.Logger.Information("Downloading Unity Hub installer for {Architecture}...", architecture);
            using (var client = new HttpClient()) {
                var response = client.GetAsync(hubUrl).Result;
                response.EnsureSuccessStatusCode();

                using (var fileStream = File.Create(tempFile)) {
                    response.Content.CopyToAsync(fileStream).Wait();
                }
            }

            GlobalConfig.Logger.Information("Installing Unity Hub...");
            var result = RunCommand("hdiutil", $"attach \"{tempFile}\" -nobrowse");
            if (result != 0) {
                GlobalConfig.Logger.Error("Failed to mount Unity Hub DMG");
                return 1;
            }

            // Copy Unity Hub to Applications
            result = RunCommand("cp", "-R \"/Volumes/Unity Hub/Unity Hub.app\" \"/Applications/\"");
            if (result != 0) {
                GlobalConfig.Logger.Error("Failed to copy Unity Hub to Applications");
                return 1;
            }

            // Unmount DMG
            RunCommand("hdiutil", "detach \"/Volumes/Unity Hub\"");

            return 0;
        }
        catch (Exception ex) {
            GlobalConfig.Logger.Error("Error installing Unity Hub: {Message}", ex.Message);
            return 1;
        }
        finally {
            try {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch {
                // Ignore cleanup errors
            }
        }
    }

    private static int InstallUnityHubLinux() {
        GlobalConfig.Logger.Warning("Please install Unity Hub manually on Linux:");
        GlobalConfig.Logger.Information("1. Download Unity Hub from https://unity3d.com/get-unity/download");
        GlobalConfig.Logger.Information("2. Extract and run the AppImage or install via your package manager");
        GlobalConfig.Logger.Information("3. Run this tool again once Unity Hub is installed");
        return 1;
    }

    private static int InstallUnityEditor(string version, bool nonInteractive = false) {
        try {
            var hubPath = GetUnityHubExecutablePath();
            if (string.IsNullOrEmpty(hubPath)) {
                GlobalConfig.Logger.Error("Unity Hub not found. Cannot install Unity Editor");
                if (nonInteractive) {
                    GlobalConfig.Logger.Error("Non-interactive mode: Exiting due to Unity Hub not found");
                    Environment.Exit(1);
                }

                return 1;
            }

            GlobalConfig.Logger.Information("Installing Unity Editor {Version} via Unity Hub CLI...", version);

            // First try without changeset, then with if we can get it
            var installArgs = $"-- --headless install --version {version}";

            // Try to get changeset but don't fail if we can't
            var changeset = GetUnityChangeset(version);
            if (!string.IsNullOrEmpty(changeset)) {
                installArgs += $" --changeset {changeset}";
                GlobalConfig.Logger.Information("Using changeset: {Changeset}", changeset);
            }
            else GlobalConfig.Logger.Information("No changeset found, trying installation without it...");

            // Use Unity Hub CLI to install the editor
            var processInfo = new ProcessStartInfo {
                FileName = hubPath,
                Arguments = installArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo)) {
                if (process == null) {
                    GlobalConfig.Logger.Error("Failed to start Unity Hub CLI");
                    return 1;
                }

                // Filter Unity Hub output - always ignore [Experiment] lines
                process.OutputDataReceived += (_, e) => {
                    if (!string.IsNullOrEmpty(e.Data) && !e.Data.TrimStart().StartsWith("[Experiment"))
                        GlobalConfig.Logger.Debug("Unity Hub: {Output}", e.Data);
                };
                process.ErrorDataReceived += (_, e) => {
                    if (!string.IsNullOrEmpty(e.Data) && !e.Data.TrimStart().StartsWith("[Experiment"))
                        GlobalConfig.Logger.Debug("Unity Hub Error: {Output}", e.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0) {
                    GlobalConfig.Logger.Error("Unity Hub CLI failed with exit code: {ExitCode}", process.ExitCode);
                    if (nonInteractive) {
                        GlobalConfig.Logger.Error("Non-interactive mode: Exiting due to Unity Hub CLI failure");
                        Environment.Exit(process.ExitCode);
                    }

                    return process.ExitCode;
                }
            }

            GlobalConfig.Logger.Information("Unity Editor {Version} installed successfully", version);
            return 0;
        }
        catch (Exception ex) {
            GlobalConfig.Logger.Error("Error installing Unity Editor: {Message}", ex.Message);
            if (nonInteractive) {
                GlobalConfig.Logger.Error("Non-interactive mode: Exiting due to error");
                Environment.Exit(1);
            }

            return 1;
        }
    }
}