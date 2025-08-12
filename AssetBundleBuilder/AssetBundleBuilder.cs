using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CryptikLemur.AssetBundleBuilder;

public static class Program {
    private static int Main(string[] args) {
        if (args.Length < 2) {
            ShowHelp();
            return 1;
        }

        var config = ArgumentParser.Parse(args);
        if (config != null) return BuildAssetBundle(config);

        Console.WriteLine("Error: Invalid arguments provided.");
        ShowHelp();
        return 1;
    }

    public static int BuildAssetBundle(BuildConfiguration config) {
        // If unity version is specified, try to find the Unity executable
        if (!string.IsNullOrEmpty(config.UnityVersion) && string.IsNullOrEmpty(config.UnityPath)) {
            config.UnityPath = UnityPathFinder.FindUnityExecutable(config.UnityVersion) ?? "";
            if (string.IsNullOrEmpty(config.UnityPath)) {
                Console.WriteLine($"Error: Could not find Unity {config.UnityVersion} installation.");
                Console.WriteLine(
                    "Searched common installation paths. Please specify the full path to Unity.exe instead.");
                return 1;
            }

            Console.WriteLine($"Found Unity {config.UnityVersion} at: {config.UnityPath}");
        }

        // Validate paths
        if (string.IsNullOrEmpty(config.UnityPath)) {
            Console.WriteLine("Error: Unity path not specified and could not be auto-detected.");
            return 1;
        }

        if (!File.Exists(config.UnityPath)) {
            Console.WriteLine($"Error: Unity executable not found at '{config.UnityPath}'");
            return 1;
        }

        if (!Directory.Exists(config.AssetDirectory)) {
            Console.WriteLine($"Error: Asset directory not found at '{config.AssetDirectory}'");
            return 1;
        }

        Console.WriteLine($"Using bundle name: {config.BundleName}");

        // Convert user-friendly build target to Unity command line format
        var unityBuildTarget = ConvertBuildTarget(config.BuildTarget);
        Console.WriteLine($"Unity build target: {unityBuildTarget}");

        // Create temporary Unity project if not specified
        if (string.IsNullOrEmpty(config.TempProjectPath)) {
            // Create hash from input parameters for consistent temp directory naming
            var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{config.BuildTarget}";
            var hash = HashUtility.ComputeHash(hashInput);
            config.TempProjectPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");
        }

        // Handle temp project cleanup if requested
        if (config.CleanTempProject && Directory.Exists(config.TempProjectPath)) {
            try {
                Directory.Delete(config.TempProjectPath, true);
                Console.WriteLine($"Cleaned up existing temp project: {config.TempProjectPath}");
            }
            catch (Exception ex) {
                Console.WriteLine($"Warning: Could not clean up existing temp project: {ex.Message}");
            }
        }

        Console.WriteLine("Starting Unity Asset Bundle Builder");
        Console.WriteLine($"Unity Path: {config.UnityPath}");
        Console.WriteLine($"Temp Project Path: {config.TempProjectPath}");
        Console.WriteLine($"Asset Directory: {config.AssetDirectory}");
        Console.WriteLine($"Bundle Name: {config.BundleName}");
        Console.WriteLine($"Output Directory: {config.OutputDirectory}");
        Console.WriteLine($"Build Target: {config.BuildTarget}");
        Console.WriteLine();

        try {
            // Ensure output directory exists
            if (!Directory.Exists(config.OutputDirectory)) {
                Directory.CreateDirectory(config.OutputDirectory);
                Console.WriteLine($"Created output directory: {config.OutputDirectory}");
            }

            // Create temporary Unity project
            CreateUnityProject(config.TempProjectPath, config.AssetDirectory, config.BundleName, config.LinkMethod);

            // Build Unity command line arguments
            var unityArgs = new[]
            {
                "-batchmode",
                "-quit",
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
                config.AssetDirectory
            };

            var processInfo = new ProcessStartInfo
            {
                FileName = config.UnityPath,
                Arguments = string.Join(" ", unityArgs.Select(arg => arg.Contains(" ") ? $"\"{arg}\"" : arg)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Console.WriteLine("Launching Unity...");
            using (var process = Process.Start(processInfo)) {
                if (process == null) throw new Exception("Launching Unity failed.");

                // Read output asynchronously
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.Error.WriteLine(e.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                if (process.ExitCode != 0) {
                    Console.WriteLine($"Error: Unity failed with exit code {process.ExitCode}");
                    return process.ExitCode;
                }
            }

            Console.WriteLine("Asset bundles built successfully!");
            return 0;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally {
            // Clean up temporary project unless requested to keep it
            if (!config.KeepTempProject && Directory.Exists(config.TempProjectPath)) {
                try {
                    Directory.Delete(config.TempProjectPath, true);
                    Console.WriteLine($"Cleaned up temporary project: {config.TempProjectPath}");
                }
                catch (Exception ex) {
                    Console.WriteLine($"Warning: Could not clean up temporary project: {ex.Message}");
                }
            }
            else if (config.KeepTempProject) Console.WriteLine($"Temporary project kept at: {config.TempProjectPath}");
        }
    }

    private static void ShowHelp() {
        Console.WriteLine("AssetBundleBuilder - Unity Asset Bundle Builder for RimWorld Mods");
        Console.WriteLine();
        Console.WriteLine(
            "Usage: AssetBundleBuilder [unity-path-or-version] <asset-directory> <output-directory> <bundle-name> [options]");
        Console.WriteLine(
            "   or: AssetBundleBuilder --unity-version <version> <asset-directory> <output-directory> <bundle-name> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  unity-path-or-version  Either:");
        Console.WriteLine(
            "                         - Path to Unity executable (e.g., C:\\Unity\\Editor\\Unity.exe)");
        Console.WriteLine(
            "                         - Unity version (e.g., 2022.3.5f1) - will auto-find executable");
        Console.WriteLine("  asset-directory        Path to the directory containing assets to bundle");
        Console.WriteLine("  output-directory       Path where the asset bundles will be created");
        Console.WriteLine("  bundle-name            Name for the asset bundle (e.g., mymod, author.modname)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --unity-version <ver>  Specify Unity version explicitly (e.g., 2022.3.5f1)");
        Console.WriteLine("  --bundle-name <name>   Override bundle name (alternative to positional argument)");
        Console.WriteLine("  --target <target>      Build target: windows, mac, or linux (default: windows)");
        Console.WriteLine("  --temp-project <path>  Custom path for temporary Unity project");
        Console.WriteLine("  --keep-temp            Keep temporary Unity project after build (for debugging)");
        Console.WriteLine("  --clean-temp           Delete existing temporary project and start over");
        Console.WriteLine("  --copy                 Copy assets to Unity project (default)");
        Console.WriteLine("  --symlink              Create symbolic link to assets (faster builds)");
        Console.WriteLine("  --hardlink             Create hard links to assets (Windows/Unix)");
        Console.WriteLine("  --junction             Create directory junction to assets (Windows only)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  AssetBundleBuilder 2022.3.5f1 \"C:\\MyMod\\Assets\" \"C:\\MyMod\\Output\" \"mymod\"");
        Console.WriteLine(
            "  AssetBundleBuilder --unity-version 2022.3.5f1 \"C:\\MyMod\\Assets\" \"C:\\MyMod\\Output\" \"author.modname\"");
        Console.WriteLine(
            "  AssetBundleBuilder \"C:\\Unity\\Editor\\Unity.exe\" \"C:\\MyMod\\Assets\" \"C:\\MyMod\\Output\" \"mymod\"");
    }

    private static void CreateUnityProject(string projectPath, string assetDirectory, string bundleName,
        string linkMethod) {
        Console.WriteLine($"Creating temporary Unity project at: {projectPath}");

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

        Console.WriteLine("Unity project created successfully.");
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
        Console.WriteLine($"Linking assets using method: {linkMethod}");
        Console.WriteLine($"Source: {sourceDirectory}");
        Console.WriteLine($"Target: {targetPath}");

        // Ensure target directory doesn't exist
        if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);

        // Ensure parent directory exists
        var parentDir = Path.GetDirectoryName(targetPath)!;
        if (!Directory.Exists(parentDir)) Directory.CreateDirectory(parentDir);

        switch (linkMethod.ToLower()) {
            case "copy":
                CopyDirectory(sourceDirectory, targetPath);
                Console.WriteLine("Assets copied successfully.");
                break;
            case "symlink":
                CreateSymbolicLink(sourceDirectory, targetPath);
                Console.WriteLine("Symbolic link created successfully.");
                break;
            case "hardlink":
                CreateHardLink(sourceDirectory, targetPath);
                Console.WriteLine("Hard links created successfully.");
                break;
            case "junction":
                CreateJunction(sourceDirectory, targetPath);
                Console.WriteLine("Junction created successfully.");
                break;
            default:
                throw new ArgumentException($"Unknown link method: {linkMethod}");
        }
    }

    // CopyDirectory helper method
    private static void CopyDirectory(string sourceDir, string destDir) {
        Directory.CreateDirectory(destDir);

        foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));

        foreach (var newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            File.Copy(newPath, newPath.Replace(sourceDir, destDir), true);
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
            processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {command} {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }
        else {
            processInfo = new ProcessStartInfo
            {
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

    private static string ConvertBuildTarget(string userBuildTarget) {
        return userBuildTarget.ToLower() switch
        {
            "windows" => "StandaloneWindows64",
            "mac" => "StandaloneOSX",
            "linux" => "StandaloneLinux64",
            _ => throw new ArgumentException($"Unsupported build target: {userBuildTarget}")
        };
    }
}