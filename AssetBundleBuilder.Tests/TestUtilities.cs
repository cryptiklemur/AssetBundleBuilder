using System.Diagnostics;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public static class TestUtilities
{
    /// <summary>
    /// Checks if a Unity installation supports a specific build target
    /// </summary>
    public static bool IsUnityBuildTargetSupported(string unityPath, string buildTarget)
    {
        if (string.IsNullOrEmpty(unityPath) || !File.Exists(unityPath))
            return false;

        try
        {
            // Try a quick Unity command to check if the build target is available
            var processInfo = new ProcessStartInfo
            {
                FileName = unityPath,
                Arguments = $"-batchmode -quit -buildTarget {ConvertBuildTargetForUnity(buildTarget)} -logFile -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null) return false;

            process.WaitForExit(30000); // 30 second timeout
            
            // Exit code 0 means the build target is supported
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if we're running in a CI environment
    /// </summary>
    public static bool IsRunningInCI()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_NUMBER"));
    }

    private static string ConvertBuildTargetForUnity(string buildTarget)
    {
        return buildTarget.ToLower() switch
        {
            "windows" => "StandaloneWindows64",
            "mac" => "StandaloneOSX", 
            "linux" => "StandaloneLinux64",
            _ => buildTarget
        };
    }
}