using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class TempDirectoryTests : IDisposable {
    private readonly List<string> _tempDirectoriesToCleanup = new();

    public void Dispose() {
        foreach (var dir in _tempDirectoriesToCleanup)
            if (Directory.Exists(dir)) {
                try {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    foreach (var file in files) File.SetAttributes(file, FileAttributes.Normal);
                    Directory.Delete(dir, true);
                }
                catch {
                    // Ignore cleanup failures in tests
                }
            }
    }

    [Fact]
    public void TempDirectory_ShouldBeCreatedFromHash() {
        var config = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows"
        };

        var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{config.BuildTarget}";
        var expectedHash = HashUtility.ComputeHash(hashInput);
        var expectedTempPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{expectedHash}");

        config.TempProjectPath = string.Empty;
        SetTempProjectPath(config);

        Assert.Equal(expectedTempPath, config.TempProjectPath);
    }

    [Fact]
    public void TempDirectory_SameInputs_ShouldProduceSamePath() {
        var config1 = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows"
        };

        var config2 = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows"
        };

        SetTempProjectPath(config1);
        SetTempProjectPath(config2);

        Assert.Equal(config1.TempProjectPath, config2.TempProjectPath);
    }

    [Fact]
    public void TempDirectory_DifferentInputs_ShouldProduceDifferentPaths() {
        var config1 = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows"
        };

        var config2 = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "different.bundle",
            BuildTarget = "windows"
        };

        SetTempProjectPath(config1);
        SetTempProjectPath(config2);

        Assert.NotEqual(config1.TempProjectPath, config2.TempProjectPath);
    }

    [Theory]
    [InlineData(@"C:\Assets", "test.bundle", "windows")]
    [InlineData(@"C:\Assets", "test.bundle", "mac")]
    [InlineData(@"C:\Assets", "test.bundle", "linux")]
    [InlineData(@"C:\Assets", "different.bundle", "windows")]
    [InlineData(@"C:\Different\Assets", "test.bundle", "windows")]
    public void TempDirectory_VariousInputs_ShouldCreateValidPaths(string assetDir, string bundleName,
        string buildTarget) {
        var config = new BuildConfiguration {
            AssetDirectory = assetDir,
            BundleName = bundleName,
            BuildTarget = buildTarget
        };

        SetTempProjectPath(config);

        Assert.False(string.IsNullOrEmpty(config.TempProjectPath));
        Assert.StartsWith(Path.GetTempPath(), config.TempProjectPath);
        Assert.Contains("AssetBundleBuilder_", config.TempProjectPath);
    }

    [Fact]
    public void TempDirectory_PreExistingPath_ShouldNotOverride() {
        var config = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows",
            TempProjectPath = @"C:\Custom\Temp\Path"
        };

        var originalPath = config.TempProjectPath;
        SetTempProjectPath(config);

        Assert.Equal(originalPath, config.TempProjectPath);
    }

    [Fact]
    public async Task CleanTempProject_ExistingDirectory_ShouldDeleteDirectory() {
        var tempDir = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "test content");
        _tempDirectoriesToCleanup.Add(tempDir);

        var config = new BuildConfiguration {
            TempProjectPath = tempDir,
            CleanTempProject = true
        };

        var success = CleanExistingTempProject(config);

        Assert.True(success);
        Assert.False(Directory.Exists(tempDir));
    }

    [Fact]
    public void CleanTempProject_NonExistentDirectory_ShouldReturnTrue() {
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_NonExistent_{Guid.NewGuid():N}");

        var config = new BuildConfiguration {
            TempProjectPath = nonExistentDir,
            CleanTempProject = true
        };

        var success = CleanExistingTempProject(config);

        Assert.True(success);
    }

    [Fact]
    public async Task CleanTempProject_ReadOnlyFiles_ShouldHandleGracefully() {
        var tempDir = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_ReadOnly_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var readOnlyFile = Path.Combine(tempDir, "readonly.txt");
        await File.WriteAllTextAsync(readOnlyFile, "readonly content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
        _tempDirectoriesToCleanup.Add(tempDir);

        var config = new BuildConfiguration {
            TempProjectPath = tempDir,
            CleanTempProject = true
        };

        var success = CleanExistingTempProject(config);

        Assert.True(success);
        Assert.False(Directory.Exists(tempDir));
    }

    [Fact]
    public void DefaultBehavior_ShouldNotCleanupTempProject() {
        var config = new BuildConfiguration {
            CleanTempProject = false
        };

        var shouldCleanup = ShouldCleanupTempProject(config);

        Assert.False(shouldCleanup);
    }

    [Fact]
    public void CleanTempFlag_ShouldCleanupTempProject() {
        var config = new BuildConfiguration {
            CleanTempProject = true
        };

        var shouldCleanup = ShouldCleanupTempProject(config);

        Assert.True(shouldCleanup);
    }

    [Fact]
    public void TempDirectoryHash_ShouldBe8Characters() {
        var config = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows"
        };

        SetTempProjectPath(config);

        var dirName = Path.GetFileName(config.TempProjectPath);
        var hashPart = dirName.Replace("AssetBundleBuilder_", "");

        Assert.Equal(8, hashPart.Length);
        Assert.All(hashPart, c => Assert.True(char.IsLetterOrDigit(c)));
    }

    private static void SetTempProjectPath(BuildConfiguration config) {
        if (string.IsNullOrEmpty(config.TempProjectPath)) {
            var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{config.BuildTarget}";
            var hash = HashUtility.ComputeHash(hashInput);
            config.TempProjectPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");
        }
    }

    private static bool CleanExistingTempProject(BuildConfiguration config) {
        if (config.CleanTempProject && Directory.Exists(config.TempProjectPath)) {
            try {
                var files = Directory.GetFiles(config.TempProjectPath, "*", SearchOption.AllDirectories);
                foreach (var file in files) File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(config.TempProjectPath, true);
                return true;
            }
            catch {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldCleanupTempProject(BuildConfiguration config) {
        return config.CleanTempProject;
    }
}