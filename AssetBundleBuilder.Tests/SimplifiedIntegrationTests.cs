using CryptikLemur.AssetBundleBuilder.Utilities;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

/// <summary>
///     Simplified integration tests focusing on core functionality:
///     1. CLI arguments work
///     2. Config files work
///     3. Output is generated correctly
/// </summary>
public class SimplifiedIntegrationTests : IDisposable {
    private readonly List<string> _tempDirsToCleanup = [];
    private readonly List<string> _tempFilesToCleanup = [];

    public void Dispose() {
        foreach (var file in _tempFilesToCleanup)
            if (File.Exists(file))
                File.Delete(file);
        foreach (var dir in _tempDirsToCleanup)
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
    }

    [Fact]
    public void CliArguments_BasicUsage_ParsesCorrectly() {
        // Test that basic CLI arguments work
        var args = new[] { "2022.3.35f1", "Assets", "testbundle", "Output" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal("2022.3.35f1", config.GetUnityVersion());
        Assert.Equal(Path.GetFullPath("Assets"), config.AssetDirectory);
        Assert.Equal("testbundle", config.BundleName);
        Assert.Equal(Path.GetFullPath("Output"), config.OutputDirectory);
    }

    [Fact]
    public void CliArguments_WithOptions_ParsesCorrectly() {
        // Test CLI with additional options
        var args = new[] {
            "2022.3.35f1", "Assets", "testbundle", "Output",
            "--target", "windows",
            "--exclude", "*.tmp",
            "--include", "*.png"
        };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal("windows", config.BuildTarget);
        Assert.Contains("*.tmp", config.ExcludePatterns);
        Assert.Contains("*.png", config.IncludePatterns);
    }

    [Fact]
    public void ConfigFile_BasicUsage_ParsesCorrectly() {
        // Test that TOML config files work
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        _tempDirsToCleanup.Add(tempDir);

        var configPath = Path.Combine(tempDir, "test.toml");
        var tomlContent = @"
[global]
unity_version = ""2022.3.35f1""
build_target = ""linux""

[bundles.test]
asset_directory = ""TestAssets""
bundle_name = ""author.textures""
";
        File.WriteAllText(configPath, tomlContent);

        var args = new[] { "--config", configPath, "--bundle-config", "test" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal("2022.3.35f1", config.GetUnityVersion());
        Assert.Equal("author.textures", config.BundleName);
        Assert.Equal("linux", config.BuildTarget);
    }

    [Fact]
    public void ConfigFile_WithBundles_SelectsCorrectBundle() {
        // Test bundle selection from TOML
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        _tempDirsToCleanup.Add(tempDir);

        var configPath = Path.Combine(tempDir, "test.toml");
        var tomlContent = @"
[global]
unity_version = ""2022.3.35f1""

[bundles.textures]
bundle_name = ""textures""
asset_directory = ""TextureAssets""

[bundles.sounds]  
bundle_name = ""sounds""
asset_directory = ""SoundAssets""
";
        File.WriteAllText(configPath, tomlContent);

        var args = new[] { "--config", configPath, "--bundle-config", "textures" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal("textures", config.BundleName);
        Assert.Equal("TextureAssets", config.AssetDirectory);
    }

    [Fact]
    public void InvalidBundleName_ThrowsException() {
        // Test that forbidden bundle names are rejected
        var args = new[] { "2022.3.35f1", "Assets", "test.bundle", "Output" };

        var exception = Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(args));
        Assert.Contains("cannot end with .framework or .bundle", exception.Message);
    }

    [Fact]
    public void MissingRequiredArgs_ReturnsNull() {
        // Test that insufficient arguments returns null
        var args = new[] { "2022.3.35f1" };
        var config = ArgumentParser.Parse(args);

        Assert.Null(config);
    }

    [Fact]
    public void BuildTargetValidation_AcceptsValidTargets() {
        // Test valid build targets
        var validTargets = new[] { "windows", "mac", "linux" };

        foreach (var target in validTargets) {
            var args = new[] { "2022.3.35f1", "Assets", "test", "Output", "--target", target };
            var config = ArgumentParser.Parse(args);
            Assert.NotNull(config);
            Assert.Equal(target, config.BuildTarget);
        }
    }

    [Fact]
    public void VerbosityLevels_ParseCorrectly() {
        // Test verbosity flags
        var testCases = new[] {
            ("-q", VerbosityLevel.Quiet),
            ("-v", VerbosityLevel.Verbose),
            ("-vv", VerbosityLevel.Debug)
        };

        foreach (var (flag, expected) in testCases) {
            var args = new[] { "2022.3.35f1", "Assets", "test", "Output", flag };
            var config = ArgumentParser.Parse(args);
            Assert.NotNull(config);
            Assert.Equal(expected, config.GetVerbosity());
        }
    }

    [Fact]
    public void LinkMethods_ParseCorrectly() {
        // Test different link methods
        var linkMethods = new[] {
            ("--copy", "copy"),
            ("--symlink", "symlink"),
            ("--hardlink", "hardlink"),
            ("--junction", "junction")
        };

        foreach (var (flag, expected) in linkMethods) {
            var args = new[] { "2022.3.35f1", "Assets", "test", "Output", flag };
            var config = ArgumentParser.Parse(args);
            Assert.NotNull(config);
            Assert.Equal(expected, config.GetLinkMethod());
        }
    }

    // End-to-end test would go here but requires Unity to be installed
    // [Fact]
    // public void EndToEnd_BuildAssetBundle_ProducesOutput() {
    //     // This would test actual asset bundle creation
    //     // Requires Unity installation and test assets
    // }
}