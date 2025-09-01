using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CryptikLemur.AssetBundleBuilder.Utilities;

public static class UnityPathFinder {
    public static string FindUnityExecutable(Configuration config, bool dontInstall = false) {
        while (true) {
            var searchPaths = GetUnitySearchPaths();

            foreach (string basePath in searchPaths.Where(Directory.Exists)) {
                string versionPath = Path.Combine(basePath, config.TomlConfig.Global.UnityVersion);
                string? unityExe = GetUnityExecutablePath(versionPath);
                if (!string.IsNullOrEmpty(unityExe) && File.Exists(unityExe)) {
                    return unityExe;
                }
            }

            if (config.CiMode || dontInstall || PromptAndInstallUnity(config) > 0) {
                return string.Empty;
            }

            dontInstall = true;
        }
    }

    private static int PromptAndInstallUnity(Configuration config) {
        string version = config.TomlConfig.Global.UnityVersion;
        Program.Logger.Warning("Unity {Version} was not found on this system", version);

        if (config.NonInteractive) {
            Program.Logger.Information(
                "Non-interactive mode: Automatically installing Unity Hub and Unity Editor...");
        }
        else {
            Console.WriteLine("Would you like to automatically download and install Unity Hub and Unity Editor? (Y/n)");
            string? response = Console.ReadLine()?.Trim().ToLower();
            if (response is "n" or "no") {
                Program.Logger.Information("Installation cancelled by user");
                return 1;
            }
        }

        try {
            // Step 1: Install Unity Hub if not present
            if (!IsUnityHubInstalled(config)) {
                Program.Logger.Information("Installing Unity Hub...");
                int hubResult = InstallUnityHub();
                if (hubResult != 0) {
                    Program.Logger.Error("Failed to install Unity Hub");
                    if (!config.NonInteractive) {
                        return hubResult;
                    }

                    Program.Logger.Error(
                        "Non-interactive mode: Exiting due to Unity Hub installation failure");
                    return hubResult;
                }

                Program.Logger.Information("Unity Hub installed successfully");
            }
            else {
                Program.Logger.Information("Unity Hub is already installed");
            }

            // Step 2: Install Unity Editor version via Hub
            Program.Logger.Information("Installing Unity Editor {Version}...", version);
            int editorResult = InstallUnityEditor(config);
            if (editorResult != 0) {
                Program.Logger.Error("Failed to install Unity Editor {Version}", version);
                if (!config.NonInteractive) {
                    return editorResult;
                }

                Program.Logger.Error("Non-interactive mode: Exiting due to Unity Editor installation failure");
                return editorResult;
            }

            Program.Logger.Information("Unity Editor {Version} installed successfully", version);
            return 0;
        }
        catch (Exception ex) {
            Program.Logger.Error("Error during Unity installation: {Message}", ex.Message);
            return 1;
        }
    }

    private static bool IsUnityHubInstalled(Configuration config) {
        return GetUnityHubExecutablePath(config) != null;
    }

    public static string? GetUnityHubExecutablePath(Configuration config) {
        if (!string.IsNullOrEmpty(config.TomlConfig.Global.UnityHubPath)) {
            if (!File.Exists(config.TomlConfig.Global.UnityHubPath)) {
                throw new FileNotFoundException("Unity Hub path not found", config.TomlConfig.Global.UnityHubPath);
            }

            return config.TomlConfig.Global.UnityHubPath;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string[] hubPaths = [
                @"C:\Program Files\Unity Hub\Unity Hub.exe",
                @"C:\Program Files (x86)\Unity Hub\Unity Hub.exe"
            ];
            return hubPaths.FirstOrDefault(File.Exists);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            string hubPath = "/Applications/Unity Hub.app/Contents/MacOS/Unity Hub";
            return File.Exists(hubPath) ? hubPath : null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            // Check common Linux installation paths
            string[] hubPaths = [
                "/opt/unityhub/unityhub",
                "/usr/bin/unityhub"
            ];
            return hubPaths.FirstOrDefault(File.Exists);
        }

        return null;
    }

    private static List<string> GetUnitySearchPaths() {
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            paths.Add(@"C:\Program Files\Unity\Hub\Editor");
            paths.Add(@"C:\Program Files (x86)\Unity\Hub\Editor");
            paths.Add(@"C:\Program Files\Unity");
            paths.Add(@"C:\Program Files (x86)\Unity");

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile)) {
                paths.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            paths.Add("/Applications/Unity/Hub/Editor");
            paths.Add("/Applications/Unity");

            string? home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home)) {
                paths.Add(Path.Combine(home, "Applications", "Unity", "Hub", "Editor"));
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            paths.Add("/opt/unity/editor");
            paths.Add("/usr/share/unity");

            string? home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home)) {
                paths.Add(Path.Combine(home, "Unity", "Hub", "Editor"));
                paths.Add(Path.Combine(home, ".local", "share", "Unity", "Hub", "Editor"));
            }
        }

        return paths;
    }

    private static string? GetUnityExecutablePath(string unityInstallPath) {
        if (!Directory.Exists(unityInstallPath)) {
            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string editorPath = Path.Combine(unityInstallPath, "Editor", "Unity.exe");
            if (File.Exists(editorPath)) {
                return editorPath;
            }

            string directPath = Path.Combine(unityInstallPath, "Unity.exe");
            if (File.Exists(directPath)) {
                return directPath;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            string appPath = Path.Combine(unityInstallPath, "Unity.app", "Contents", "MacOS", "Unity");
            if (File.Exists(appPath)) {
                return appPath;
            }

            string altPath = Path.Combine(unityInstallPath, "Contents", "MacOS", "Unity");
            if (File.Exists(altPath)) {
                return altPath;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            string editorPath = Path.Combine(unityInstallPath, "Editor", "Unity");
            if (File.Exists(editorPath)) {
                return editorPath;
            }

            string directPath = Path.Combine(unityInstallPath, "Unity");
            if (File.Exists(directPath)) {
                return directPath;
            }
        }

        return null;
    }


    private static int InstallUnityHub() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return InstallUnityHubWindows();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return InstallUnityHubMac();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return InstallUnityHubLinux();
        }

        Program.Logger.Error("Unsupported platform for automatic Unity Hub installation");
        return 1;
    }

    private static int InstallUnityHubWindows() {
        string hubUrl = "https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe";
        string tempFile = Path.Combine(Path.GetTempPath(), "UnityHubSetup.exe");

        try {
            // Download Unity Hub installer
            Program.Logger.Information("Downloading Unity Hub installer...");
            using (var client = new HttpClient()) {
                var response = client.GetAsync(hubUrl).Result;
                response.EnsureSuccessStatusCode();

                using (var fileStream = File.Create(tempFile)) {
                    response.Content.CopyToAsync(fileStream).Wait();
                }
            }

            // Run the installer silently
            Program.Logger.Information("Running Unity Hub installer...");
            var processInfo = new ProcessStartInfo {
                FileName = tempFile,
                Arguments = "/S", // Silent installation
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo)) {
                if (process == null) {
                    Program.Logger.Error("Failed to start Unity Hub installer");
                    return 1;
                }

                process.WaitForExit();

                if (process.ExitCode != 0) {
                    Program.Logger.Error("Unity Hub installer failed with exit code: {ExitCode}",
                        process.ExitCode);
                    return process.ExitCode;
                }
            }

            return 0;
        }
        catch (Exception ex) {
            Program.Logger.Error("Error installing Unity Hub: {Message}", ex.Message);
            return 1;
        }
        finally {
            // Clean up temp file
            try {
                if (File.Exists(tempFile)) {
                    File.Delete(tempFile);
                }
            }
            catch {
                // Ignore cleanup errors
            }
        }
    }

    private static int InstallUnityHubMac() {
        string architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "darwin-arm64" : "darwin-x64";
        string hubUrl = $"https://public-cdn.cloud.unity3d.com/hub/prod/UnityHub-{architecture}.dmg";
        string tempFile = Path.Combine(Path.GetTempPath(), "UnityHub.dmg");

        try {
            Program.Logger.Information("Downloading Unity Hub installer for {Architecture}...", architecture);
            using (var client = new HttpClient()) {
                var response = client.GetAsync(hubUrl).Result;
                response.EnsureSuccessStatusCode();

                using (var fileStream = File.Create(tempFile)) {
                    response.Content.CopyToAsync(fileStream).Wait();
                }
            }

            Program.Logger.Information("Installing Unity Hub...");
            int result = Program.RunCommand("hdiutil", $"attach \"{tempFile}\" -nobrowse");
            if (result != 0) {
                Program.Logger.Error("Failed to mount Unity Hub DMG");
                return 1;
            }

            // Copy Unity Hub to Applications
            result = Program.RunCommand("cp", "-R \"/Volumes/Unity Hub/Unity Hub.app\" \"/Applications/\"");
            if (result != 0) {
                Program.Logger.Error("Failed to copy Unity Hub to Applications");
                return 1;
            }

            // Unmount DMG
            Program.RunCommand("hdiutil", "detach \"/Volumes/Unity Hub\"");

            return 0;
        }
        catch (Exception ex) {
            Program.Logger.Error("Error installing Unity Hub: {Message}", ex.Message);
            return 1;
        }
        finally {
            try {
                if (File.Exists(tempFile)) {
                    File.Delete(tempFile);
                }
            }
            catch {
                // Ignore cleanup errors
            }
        }
    }

    private static int InstallUnityHubLinux() {
        Program.Logger.Warning("Please install Unity Hub manually on Linux:");
        Program.Logger.Information("1. Download Unity Hub from https://unity3d.com/get-unity/download");
        Program.Logger.Information("2. Extract and run the AppImage or install via your package manager");
        Program.Logger.Information("3. Run this tool again once Unity Hub is installed");
        return 1;
    }

    private static string? GetUnityChangeset(string version) {
        try {
            string url = $"https://services.api.unity.com/unity/editor/release/v1/releases?version={version}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AssetBundleBuilder");

            var response = client.GetAsync(url).Result;

            if (!response.IsSuccessStatusCode) {
                return null;
            }

            string responseContent = response.Content.ReadAsStringAsync().Result;

            // Parse JSON to extract changeset from shortRevision
            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0) {
                var firstResult = results[0];
                if (firstResult.TryGetProperty("shortRevision", out var shortRevision)) {
                    string? changesetValue = shortRevision.GetString();
                    if (!string.IsNullOrEmpty(changesetValue)) {
                        Program.Logger.Debug("Found changeset: {Changeset}", changesetValue);
                        return changesetValue;
                    }
                }
            }

            Program.Logger.Debug("No changeset found in API response");
            return null;
        }
        catch (Exception ex) {
            Program.Logger.Warning("Error querying Unity changeset via REST API: {Message}", ex.Message);
            return null;
        }
    }

    private static int InstallUnityEditor(Configuration config) {
        try {
            string version = config.TomlConfig.Global.UnityVersion;
            string? hubPath = GetUnityHubExecutablePath(config);
            if (string.IsNullOrEmpty(hubPath)) {
                Program.Logger.Error("Unity Hub not found. Cannot install Unity Editor");
                if (config.NonInteractive) {
                    Program.Logger.Error("Non-interactive mode: Exiting due to Unity Hub not found");
                    Environment.Exit(1);
                }

                return 1;
            }

            Program.Logger.Information("Installing Unity Editor {Version} via Unity Hub CLI...", version);

            // First try without changeset, then with if we can get it
            string installArgs = $"-- --headless install --version {version}";

            // Try to get changeset but don't fail if we can't
            string? changeset = GetUnityChangeset(version);
            if (!string.IsNullOrEmpty(changeset)) {
                installArgs += $" --changeset {changeset}";
                Program.Logger.Information("Using changeset: {Changeset}", changeset);
            }
            else {
                Program.Logger.Information("No changeset found, trying installation without it...");
            }

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
                    Program.Logger.Error("Failed to start Unity Hub CLI");
                    return 1;
                }

                // Filter Unity Hub output - always ignore [Experiment] lines
                process.OutputDataReceived += (_, e) => {
                    if (!string.IsNullOrEmpty(e.Data) && !e.Data.TrimStart().StartsWith("[Experiment")) {
                        Program.Logger.Debug("Unity Hub: {Output}", e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) => {
                    if (!string.IsNullOrEmpty(e.Data) && !e.Data.TrimStart().StartsWith("[Experiment")) {
                        Program.Logger.Debug("Unity Hub Error: {Output}", e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0) {
                    Program.Logger.Error("Unity Hub CLI failed with exit code: {ExitCode}", process.ExitCode);
                    if (config.NonInteractive) {
                        Program.Logger.Error("Non-interactive mode: Exiting due to Unity Hub CLI failure");
                        Environment.Exit(process.ExitCode);
                    }

                    return process.ExitCode;
                }
            }

            Program.Logger.Information("Unity Editor {Version} installed successfully", version);
            return 0;
        }
        catch (Exception ex) {
            Program.Logger.Error("Error installing Unity Editor: {Message}", ex.Message);
            if (config.NonInteractive) {
                Program.Logger.Error("Non-interactive mode: Exiting due to error");
                Environment.Exit(1);
            }

            return 1;
        }
    }
}