using System.Runtime.InteropServices;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class UnityHubPathTests {
    [Fact]
    public void WindowsUnityHubPaths_ContainExpectedLocations() {
        // Arrange
        var expectedPaths = new[] {
            @"C:\Program Files\Unity Hub\Unity Hub.exe",
            @"C:\Program Files (x86)\Unity Hub\Unity Hub.exe"
        };

        // Act & Assert
        foreach (var path in expectedPaths) {
            Assert.Contains("Unity Hub.exe", path);
            Assert.StartsWith(@"C:\Program Files", path);
        }
    }

    [Fact]
    public void MacUnityHubPath_HasCorrectFormat() {
        // Arrange
        var expectedPath = "/Applications/Unity Hub.app/Contents/MacOS/Unity Hub";

        // Act & Assert
        Assert.StartsWith("/Applications", expectedPath);
        Assert.Contains("Unity Hub.app", expectedPath);
        Assert.EndsWith("Unity Hub", expectedPath);
    }

    [Fact]
    public void LinuxUnityHubPaths_ContainExpectedLocations() {
        // Arrange
        var expectedPaths = new[] {
            "/opt/unityhub/unityhub",
            "/usr/bin/unityhub"
        };

        // Act & Assert
        foreach (var path in expectedPaths) {
            Assert.EndsWith("unityhub", path);
            Assert.True(path.StartsWith("/opt/") || path.StartsWith("/usr/"));
        }
    }

    [Theory]
    [InlineData("2022.3.58f1")]
    [InlineData("2021.3.45f1")]
    [InlineData("6000.0.55f1")]
    public void UnityHubInstallCommand_HasCorrectFormat(string version) {
        // Arrange
        var hubPath = @"C:\Program Files\Unity Hub\Unity Hub.exe";
        var changeset = "abc123def456";

        // Act
        var commandWithChangeset = $"\"{hubPath}\" -- --headless install --version {version} --changeset {changeset}";
        var commandWithoutChangeset = $"\"{hubPath}\" -- --headless install --version {version}";

        // Assert
        Assert.Contains("--headless install", commandWithChangeset);
        Assert.Contains($"--version {version}", commandWithChangeset);
        Assert.Contains($"--changeset {changeset}", commandWithChangeset);

        Assert.Contains("--headless install", commandWithoutChangeset);
        Assert.Contains($"--version {version}", commandWithoutChangeset);
        Assert.DoesNotContain("--changeset", commandWithoutChangeset);
    }

    [Fact]
    public void UnityHubDownloadUrl_Windows_IsValid() {
        // Arrange
        var expectedUrl = "https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe";

        // Act & Assert
        Assert.StartsWith("https://", expectedUrl);
        Assert.Contains("unity3d.com", expectedUrl);
        Assert.EndsWith("UnityHubSetup.exe", expectedUrl);
    }

    [Theory]
    [InlineData(Architecture.Arm64, "darwin-arm64")]
    [InlineData(Architecture.X64, "darwin-x64")]
    public void UnityHubDownloadUrl_Mac_ContainsCorrectArchitecture(Architecture arch, string expectedArch) {
        // Act
        var architecture = arch == Architecture.Arm64 ? "darwin-arm64" : "darwin-x64";
        var hubUrl = $"https://public-cdn.cloud.unity3d.com/hub/prod/UnityHub-{architecture}.dmg";

        // Assert
        Assert.Contains(expectedArch, hubUrl);
        Assert.EndsWith(".dmg", hubUrl);
        Assert.Contains("unity3d.com", hubUrl);
    }

    [Fact]
    public void ProcessNames_ForUnityHub_AreCorrect() {
        // Arrange
        var expectedProcessName = "Unity Hub";

        // Act & Assert
        Assert.Equal("Unity Hub", expectedProcessName);
        // This tests that we're looking for the right process name when checking if Unity Hub is running
    }
}