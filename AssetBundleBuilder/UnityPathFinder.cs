using System.Runtime.InteropServices;

namespace CryptikLemur.AssetBundleBuilder;

public static class UnityPathFinder
{
    public static string? FindUnityExecutable(string version)
    {
        var searchPaths = GetUnitySearchPaths();

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            var versionPath = Path.Combine(basePath, version);
            var unityExe = GetUnityExecutablePath(versionPath);
            if (!string.IsNullOrEmpty(unityExe) && File.Exists(unityExe))
            {
                return unityExe;
            }

            try
            {
                var directories = Directory.GetDirectories(basePath)
                    .Select(d => Path.GetFileName(d))
                    .Where(d => d.StartsWith(version.Split('.')[0]) && d.Contains(version.Split('.')[1]))
                    .OrderByDescending(d => d)
                    .ToArray();

                foreach (var dir in directories)
                {
                    var candidatePath = Path.Combine(basePath, dir);
                    unityExe = GetUnityExecutablePath(candidatePath);
                    if (!string.IsNullOrEmpty(unityExe) && File.Exists(unityExe))
                    {
                        Console.WriteLine($"Using closest match: {dir} for requested version {version}");
                        return unityExe;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error searching {basePath}: {ex.Message}");
            }
        }

        return null;
    }

    public static List<string> GetUnitySearchPaths()
    {
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            paths.Add(@"C:\Program Files\Unity\Hub\Editor");
            paths.Add(@"C:\Program Files (x86)\Unity\Hub\Editor");
            paths.Add(@"C:\Program Files\Unity");
            paths.Add(@"C:\Program Files (x86)\Unity");

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                paths.Add(Path.Combine(userProfile, "Unity", "Hub", "Editor"));
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            paths.Add("/Applications/Unity/Hub/Editor");
            paths.Add("/Applications/Unity");

            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                paths.Add(Path.Combine(home, "Applications", "Unity", "Hub", "Editor"));
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            paths.Add("/opt/unity/editor");
            paths.Add("/usr/share/unity");

            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                paths.Add(Path.Combine(home, "Unity", "Hub", "Editor"));
                paths.Add(Path.Combine(home, ".local", "share", "Unity", "Hub", "Editor"));
            }
        }

        return paths;
    }

    public static string? GetUnityExecutablePath(string unityInstallPath)
    {
        if (!Directory.Exists(unityInstallPath)) return null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var editorPath = Path.Combine(unityInstallPath, "Editor", "Unity.exe");
            if (File.Exists(editorPath)) return editorPath;

            var directPath = Path.Combine(unityInstallPath, "Unity.exe");
            if (File.Exists(directPath)) return directPath;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var appPath = Path.Combine(unityInstallPath, "Unity.app", "Contents", "MacOS", "Unity");
            if (File.Exists(appPath)) return appPath;

            var altPath = Path.Combine(unityInstallPath, "Contents", "MacOS", "Unity");
            if (File.Exists(altPath)) return altPath;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var editorPath = Path.Combine(unityInstallPath, "Editor", "Unity");
            if (File.Exists(editorPath)) return editorPath;

            var directPath = Path.Combine(unityInstallPath, "Unity");
            if (File.Exists(directPath)) return directPath;
        }

        return null;
    }
}