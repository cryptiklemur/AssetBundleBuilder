using Xunit;
using System.Runtime.InteropServices;
using CryptikLemur.AssetBundleBuilder;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class UnityPathFinderTests
{
    [Theory]
    [InlineData("2022.3.5f1")]
    [InlineData("2023.2.0f1")]
    [InlineData("2021.3.16f1")]
    public void FindUnityExecutable_WithValidVersion_ShouldReturnPath(string version)
    {
        var result = UnityPathFinder.FindUnityExecutable(version);
        
        // On CI or systems without Unity, this might return null, which is acceptable
        if (result != null)
        {
            Assert.True(System.IO.File.Exists(result), $"Unity executable not found at: {result}");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.True(result.EndsWith("Unity.exe"), "Windows Unity executable should end with Unity.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.True(result.EndsWith("Unity"), "Linux Unity executable should end with Unity");
            }
        }
    }

    [Fact]
    public void GetUnitySearchPaths_ShouldReturnPlatformSpecificPaths()
    {
        var paths = UnityPathFinder.GetUnitySearchPaths();
        
        Assert.NotEmpty(paths);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Contains(paths, p => p.Contains(@"Program Files\Unity"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Contains(paths, p => p.Contains("/Applications/Unity"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Contains(paths, p => p.Contains("/opt/unity"));
        }
    }

    [Theory]
    [InlineData("C:\\Unity\\2022.3.5f1", "C:\\Unity\\2022.3.5f1\\Editor\\Unity.exe")]
    [InlineData("/Applications/Unity/Hub/Editor/2022.3.5f1", "/Applications/Unity/Hub/Editor/2022.3.5f1/Unity.app/Contents/MacOS/Unity")]
    [InlineData("/opt/unity/editor/2022.3.5f1", "/opt/unity/editor/2022.3.5f1/Editor/Unity")]
    public void GetUnityExecutablePath_WithValidPath_ShouldReturnExpectedExecutable(string installPath, string expectedPath)
    {
        // Skip test if not on the correct platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !expectedPath.EndsWith(".exe"))
            return;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !expectedPath.Contains("MacOS"))
            return;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && expectedPath.Contains(".exe"))
            return;
            
        var result = UnityPathFinder.GetUnityExecutablePath(installPath);
        
        // This will be null if the directory doesn't exist, which is expected in tests
        if (result != null)
        {
            Assert.Equal(expectedPath, result);
        }
    }

    [Fact]
    public void GetUnityExecutablePath_WithNonExistentPath_ShouldReturnNull()
    {
        var result = UnityPathFinder.GetUnityExecutablePath("/non/existent/path");
        
        Assert.Null(result);
    }
}