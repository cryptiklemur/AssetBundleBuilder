using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class ConfigurationLoaderTests : IDisposable {
    private readonly string _tempDir;

    public ConfigurationLoaderTests() {
        _tempDir = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() {
        if (Directory.Exists(_tempDir)) {
            try {
                Directory.Delete(_tempDir, true);
            }
            catch {
                // Ignore cleanup errors in tests
            }
        }
    }

    private string CreateTestAssetDirectory() {
        var assetDir = Path.Combine(_tempDir, "TestAssets");
        Directory.CreateDirectory(assetDir);
        File.WriteAllText(Path.Combine(assetDir, "test.png"), "fake png content");
        return assetDir;
    }

    [Fact]
    public void LoadFromToml_SingleBundle_ShouldLoadCorrectly() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[bundles.mymod]
asset_directory = ""TestAssets""
bundle_name = ""mymod""
unity_version = ""2022.3.35f1""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var config = ConfigurationLoader.LoadFromToml(configFile);

        Assert.Equal(assetDir, config.AssetDirectory);
        Assert.Equal("mymod", config.BundleName);
        Assert.Equal("2022.3.35f1", config.UnityVersion);
    }

    [Fact]
    public void LoadFromToml_MultipleBundles_WithSpecificBundle_ShouldLoadCorrectly() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[bundles.mod1]
asset_directory = ""TestAssets""
bundle_name = ""mod1""
unity_version = ""2022.3.35f1""

[bundles.mod2]
asset_directory = ""TestAssets""
bundle_name = ""mod2""
unity_version = ""2022.3.6f1""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var config = ConfigurationLoader.LoadFromToml(configFile, "mod2");

        Assert.Equal("mod2", config.BundleName);
        Assert.Equal("2022.3.6f1", config.UnityVersion);
    }

    [Fact]
    public void LoadFromToml_MultipleBundles_WithoutSpecifying_ShouldThrow() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[bundles.mod1]
asset_directory = ""TestAssets""
bundle_name = ""mod1""

[bundles.mod2]
asset_directory = ""TestAssets""
bundle_name = ""mod2""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var exception = Assert.Throws<ArgumentException>(() => ConfigurationLoader.LoadFromToml(configFile));
        Assert.Contains("Multiple bundles found", exception.Message);
        Assert.Contains("mod1, mod2", exception.Message);
    }

    [Fact]
    public void LoadFromToml_WithGlobalConfig_ShouldMergeCorrectly() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[global]
unity_version = ""2022.3.35f1""
build_target = ""windows""
clean_temp_project = true

[bundles.mymod]
asset_directory = ""TestAssets""
bundle_name = ""mymod""
build_target = ""linux""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var config = ConfigurationLoader.LoadFromToml(configFile);

        Assert.Equal("2022.3.35f1", config.UnityVersion); // From global
        Assert.Equal("linux", config.BuildTarget); // Bundle overrides global
        Assert.True(config.CleanTempProject); // From global
        Assert.Equal("mymod", config.BundleName); // From bundle
    }

    [Fact]
    public void LoadFromToml_WithCliOverrides_ShouldPrioritizeCorrectly() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[global]
unity_version = ""2022.3.35f1""
build_target = ""windows""

[bundles.mymod]
asset_directory = ""TestAssets""
bundle_name = ""mymod""
build_target = ""linux""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var cliConfig = new BuildConfiguration {
            BuildTarget = "mac",
            CleanTempProject = true
        };

        var config = ConfigurationLoader.LoadFromToml(configFile, "mymod", cliConfig);

        Assert.Equal("2022.3.35f1", config.UnityVersion); // From global
        Assert.Equal("mac", config.BuildTarget); // CLI overrides everything
        Assert.True(config.CleanTempProject); // From CLI
        Assert.Equal("mymod", config.BundleName); // From bundle
    }

    [Fact]
    public void LoadFromToml_WithRelativePaths_ShouldResolveCorrectly() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[bundles.mymod]
asset_directory = ""TestAssets""
bundle_name = ""mymod""
output_directory = ""../output""
unity_version = ""2022.3.35f1""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var config = ConfigurationLoader.LoadFromToml(configFile);

        Assert.Equal(assetDir, config.AssetDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "..", "output")), config.OutputDirectory);
    }

    [Fact]
    public void LoadFromToml_WithIncludeExcludePatterns_ShouldLoadCorrectly() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[global]
exclude_patterns = [""*.tmp"", ""backup/*""]

[bundles.mymod]
asset_directory = ""TestAssets""
bundle_name = ""mymod""
unity_version = ""2022.3.35f1""
include_patterns = [""*.png"", ""*.jpg""]
exclude_patterns = [""*.bak""]
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var config = ConfigurationLoader.LoadFromToml(configFile);

        Assert.Contains("*.png", config.IncludePatterns);
        Assert.Contains("*.jpg", config.IncludePatterns);
        Assert.Contains("*.tmp", config.ExcludePatterns); // From global
        Assert.Contains("backup/*", config.ExcludePatterns); // From global
        Assert.Contains("*.bak", config.ExcludePatterns); // From bundle
    }

    [Fact]
    public void LoadFromToml_MissingRequiredFields_ShouldThrow() {
        var configContent = @"
[bundles.incomplete]
bundle_name = ""incomplete""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var exception = Assert.Throws<ArgumentException>(() => ConfigurationLoader.LoadFromToml(configFile));
        Assert.Contains("AssetDirectory is required", exception.Message);
    }

    [Fact]
    public void LoadFromToml_InvalidBundleName_ShouldThrow() {
        var assetDir = CreateTestAssetDirectory();
        var configContent = @"
[bundles.mymod]
asset_directory = ""TestAssets""
bundle_name = ""mymod""
unity_version = ""2022.3.35f1""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var exception =
            Assert.Throws<ArgumentException>(() => ConfigurationLoader.LoadFromToml(configFile, "nonexistent"));
        Assert.Contains("Bundle 'nonexistent' not found", exception.Message);
        Assert.Contains("mymod", exception.Message);
    }

    [Fact]
    public void ListBundles_ShouldReturnAllBundleNames() {
        var configContent = @"
[bundles.mod1]
asset_directory = ""assets1""
bundle_name = ""mod1""

[bundles.mod2]
asset_directory = ""assets2""
bundle_name = ""mod2""

[bundles.mod3]
asset_directory = ""assets3""
bundle_name = ""mod3""
";

        var configFile = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(configFile, configContent);

        var bundles = ConfigurationLoader.ListBundles(configFile);

        Assert.Equal(3, bundles.Count);
        Assert.Contains("mod1", bundles);
        Assert.Contains("mod2", bundles);
        Assert.Contains("mod3", bundles);
    }
}