using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class TempDirectoryIntegrationTests : IDisposable {
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
    public async Task TempDirectory_CachingBehavior_ShouldReuseExistingDirectory() {
        var config = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows"
        };

        var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{config.BuildTarget}";
        var hash = HashUtility.ComputeHash(hashInput);
        var tempPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");

        _tempDirectoriesToCleanup.Add(tempPath);

        Directory.CreateDirectory(tempPath);
        var cacheMarkerFile = Path.Combine(tempPath, "cache_marker.txt");
        await File.WriteAllTextAsync(cacheMarkerFile, "cached content");

        config.TempProjectPath = string.Empty;
        SetTempProjectPath(config);

        Assert.Equal(tempPath, config.TempProjectPath);
        Assert.True(File.Exists(cacheMarkerFile));
    }

    [Fact]
    public async Task TempDirectory_CleanupEnabled_ShouldRemoveCachedContent() {
        var config = new BuildConfiguration {
            AssetDirectory = @"C:\Test\Assets",
            BundleName = "test.bundle",
            BuildTarget = "windows",
            CleanTempProject = true
        };

        var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{config.BuildTarget}";
        var hash = HashUtility.ComputeHash(hashInput);
        var tempPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");

        _tempDirectoriesToCleanup.Add(tempPath);

        Directory.CreateDirectory(tempPath);
        var cacheMarkerFile = Path.Combine(tempPath, "cache_marker.txt");
        await File.WriteAllTextAsync(cacheMarkerFile, "cached content");

        config.TempProjectPath = tempPath;
        var success = CleanExistingTempProject(config);

        Assert.True(success);
        Assert.False(Directory.Exists(tempPath));
        Assert.False(File.Exists(cacheMarkerFile));
    }

    [Fact]
    public void TempDirectory_MultipleConfigurations_ShouldCreateSeparateDirectories() {
        var configs = new[] {
            new BuildConfiguration { AssetDirectory = @"C:\Test1", BundleName = "bundle1", BuildTarget = "windows" },
            new BuildConfiguration { AssetDirectory = @"C:\Test2", BundleName = "bundle2", BuildTarget = "mac" },
            new BuildConfiguration { AssetDirectory = @"C:\Test3", BundleName = "bundle3", BuildTarget = "linux" }
        };

        var tempPaths = new List<string>();

        foreach (var config in configs) {
            SetTempProjectPath(config);
            tempPaths.Add(config.TempProjectPath);
            _tempDirectoriesToCleanup.Add(config.TempProjectPath);
        }

        Assert.Equal(3, tempPaths.Distinct().Count());

        foreach (var path in tempPaths) {
            Assert.Contains("AssetBundleBuilder_", path);
            Assert.StartsWith(Path.GetTempPath(), path);
        }
    }

    [Fact]
    public void TempDirectory_HashCollisionResistance_ShouldHandleSimilarInputs() {
        var configs = new[] {
            new BuildConfiguration { AssetDirectory = @"C:\Test", BundleName = "ab", BuildTarget = "windows" },
            new BuildConfiguration { AssetDirectory = @"C:\Test", BundleName = "ba", BuildTarget = "windows" },
            new BuildConfiguration { AssetDirectory = @"C:\Tes", BundleName = "tab", BuildTarget = "windows" }
        };

        var tempPaths = new List<string>();

        foreach (var config in configs) {
            SetTempProjectPath(config);
            tempPaths.Add(config.TempProjectPath);
            _tempDirectoriesToCleanup.Add(config.TempProjectPath);
        }

        Assert.Equal(3, tempPaths.Distinct().Count());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void TempDirectory_CleanupFlags_ShouldBehaveDifferently(bool cleanTemp, bool shouldCleanup) {
        var config = new BuildConfiguration {
            CleanTempProject = cleanTemp
        };

        var actualShouldCleanup = ShouldCleanupTempProject(config);

        Assert.Equal(shouldCleanup, actualShouldCleanup);
    }

    [Fact]
    public void TempDirectory_LongPath_ShouldHandleGracefully() {
        var longAssetDirectory = @"C:\" + new string('a', 200) + @"\Assets";
        var config = new BuildConfiguration {
            AssetDirectory = longAssetDirectory,
            BundleName = "test.bundle",
            BuildTarget = "windows"
        };

        SetTempProjectPath(config);

        Assert.False(string.IsNullOrEmpty(config.TempProjectPath));
        Assert.StartsWith(Path.GetTempPath(), config.TempProjectPath);

        var dirName = Path.GetFileName(config.TempProjectPath);
        var hashPart = dirName.Replace("AssetBundleBuilder_", "");
        Assert.Equal(8, hashPart.Length);

        _tempDirectoriesToCleanup.Add(config.TempProjectPath);
    }

    [Fact]
    public void TempDirectory_SpecialCharacters_ShouldProduceValidHash() {
        var config = new BuildConfiguration {
            AssetDirectory = @"C:\Test Assets & Files",
            BundleName = "special.bundle-name_v1.0",
            BuildTarget = "windows"
        };

        SetTempProjectPath(config);

        Assert.False(string.IsNullOrEmpty(config.TempProjectPath));
        Assert.StartsWith(Path.GetTempPath(), config.TempProjectPath);

        var dirName = Path.GetFileName(config.TempProjectPath);
        Assert.Contains("AssetBundleBuilder_", dirName);

        var hashPart = dirName.Replace("AssetBundleBuilder_", "");
        Assert.Equal(8, hashPart.Length);
        Assert.All(hashPart, c => Assert.True(char.IsLetterOrDigit(c)));

        _tempDirectoriesToCleanup.Add(config.TempProjectPath);
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