using System.CommandLine;
using System.CommandLine.Parsing;
using CryptikLemur.AssetBundleBuilder.Config;
using Tomlet;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

[Collection("AssetBuilder Sequential Tests")]
public class ConfigExtensionTests : IDisposable {
    private readonly ITestOutputHelper _output;
    private readonly List<string> _tempFilesToCleanup = [];
    private readonly string _testConfigDir;

    public ConfigExtensionTests(ITestOutputHelper output) {
        _output = output;
        _testConfigDir = Path.Combine(Path.GetTempPath(), $"ConfigTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testConfigDir);
    }

    public void Dispose() {
        foreach (string file in _tempFilesToCleanup) {
            try {
                if (File.Exists(file)) {
                    File.Delete(file);
                }
            }
            catch (Exception ex) {
                _output.WriteLine($"Warning: Could not delete temp file {file}: {ex.Message}");
            }
        }

        try {
            if (Directory.Exists(_testConfigDir)) {
                Directory.Delete(_testConfigDir, true);
            }
        }
        catch (Exception ex) {
            _output.WriteLine($"Warning: Could not delete temp directory {_testConfigDir}: {ex.Message}");
        }
    }

    [Fact]
    public void ExtendedConfig_ShouldInheritBaseValues() {
        // Arrange
        string baseConfigPath = CreateConfigFile("base.toml", @"
[global]
unity_version = ""2022.3.35f1""
unity_editor_path = ""/path/to/unity""
link_method = ""hardlink""
allowed_targets = [""windows"", ""mac""]

[bundles.base_bundle]
asset_directory = ""/base/assets""
output_directory = ""/base/output""
");

        string childConfigPath = CreateConfigFile("child.toml", @"
[global]
extends = ""base.toml""
unity_version = ""2023.1.0f1""

[bundles.child_bundle]
asset_directory = ""/child/assets""
output_directory = ""/child/output""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        Assert.Equal("2023.1.0f1", config.TomlConfig.Global.UnityVersion);
        Assert.Equal("/path/to/unity", config.TomlConfig.Global.UnityEditorPath);
        Assert.Equal("hardlink", config.TomlConfig.Global.LinkMethod);
        Assert.Contains("windows", config.TomlConfig.Global.AllowedTargets);
        Assert.Contains("mac", config.TomlConfig.Global.AllowedTargets);

        // Should have both bundles
        Assert.Equal(2, config.TomlConfig.Bundles.Count);
        Assert.True(config.TomlConfig.Bundles.ContainsKey("base_bundle"));
        Assert.True(config.TomlConfig.Bundles.ContainsKey("child_bundle"));
    }

    [Fact]
    public void ExtendedConfig_ShouldOverrideBundleWithSameName() {
        // Arrange
        string baseConfigPath = CreateConfigFile("base.toml", @"
[global]
unity_version = ""2022.3.35f1""

[bundles.my_bundle]
asset_directory = ""/base/assets""
output_directory = ""/base/output""
bundle_name = ""base.bundle""
");

        string childConfigPath = CreateConfigFile("child.toml", @"
[global]
extends = ""base.toml""

[bundles.my_bundle]
asset_directory = ""/child/assets""
output_directory = ""/child/output""
bundle_name = ""child.bundle""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        Assert.Single(config.TomlConfig.Bundles);
        var bundle = config.TomlConfig.Bundles["my_bundle"];
        Assert.Equal("/child/assets", bundle.AssetDirectory);
        Assert.Equal("/child/output", bundle.OutputDirectory);
        Assert.Equal("child.bundle", bundle.BundleName);
    }

    [Fact]
    public void ExtendedConfig_ShouldSupportNestedExtends() {
        // Arrange
        string grandparentConfigPath = CreateConfigFile("grandparent.toml", @"
[global]
unity_version = ""2021.3.0f1""
unity_editor_path = ""/grandparent/unity""
link_method = ""symlink""
");

        string parentConfigPath = CreateConfigFile("parent.toml", @"
[global]
extends = ""grandparent.toml""
unity_version = ""2022.3.0f1""
link_method = ""hardlink""
");

        string childConfigPath = CreateConfigFile("child.toml", @"
[global]
extends = ""parent.toml""
unity_version = ""2023.1.0f1""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        Assert.Equal("2023.1.0f1", config.TomlConfig.Global.UnityVersion);
        Assert.Equal("/grandparent/unity", config.TomlConfig.Global.UnityEditorPath);
        Assert.Equal("hardlink", config.TomlConfig.Global.LinkMethod);
    }

    [Fact]
    public void ExtendedConfig_ShouldDetectCircularReferences() {
        // Arrange
        string config1Path = CreateConfigFile("config1.toml", @"
[global]
extends = ""config2.toml""
unity_version = ""2023.1.0f1""
");

        string config2Path = CreateConfigFile("config2.toml", @"
[global]
extends = ""config1.toml""
unity_version = ""2023.1.0f1""
");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            LoadConfigurationFromFile(config1Path));
        Assert.Contains("Circular reference detected", exception.Message);
    }

    [Fact]
    public void ExtendedConfig_ShouldHandleRelativePaths() {
        // Arrange
        string subDir = Path.Combine(_testConfigDir, "configs");
        Directory.CreateDirectory(subDir);

        string baseConfigPath = CreateConfigFile("base.toml", @"
[global]
unity_version = ""2022.3.35f1""
");

        string childConfigPath = CreateConfigFile(Path.Combine("configs", "child.toml"), @"
[global]
extends = ""../base.toml""
unity_version = ""2023.1.0f1""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        Assert.Equal("2023.1.0f1", config.TomlConfig.Global.UnityVersion);
    }

    [Fact]
    public void ExtendedConfig_ShouldThrowWhenExtendedFileNotFound() {
        // Arrange
        string configPath = CreateConfigFile("config.toml", @"
[global]
extends = ""nonexistent.toml""
unity_version = ""2023.1.0f1""
");

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
            LoadConfigurationFromFile(configPath));
        Assert.Contains("Configuration file not found", exception.Message);
    }

    [Fact]
    public void ExtendedConfig_ShouldLoadSameNamedConfigFromDirectory() {
        // Arrange
        string parentDir = Path.Combine(_testConfigDir, "parent");
        Directory.CreateDirectory(parentDir);

        string parentConfigPath = CreateConfigFile(Path.Combine("parent", "myconfig.toml"), @"
[global]
unity_version = ""2022.3.35f1""
unity_editor_path = ""/parent/unity""

[bundles.parent_bundle]
asset_directory = ""/parent/assets""
");

        string childConfigPath = CreateConfigFile("myconfig.toml", @"
[global]
extends = ""parent""
unity_version = ""2023.1.0f1""

[bundles.child_bundle]
asset_directory = ""/child/assets""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        Assert.Equal("2023.1.0f1", config.TomlConfig.Global.UnityVersion);
        Assert.Equal("/parent/unity", config.TomlConfig.Global.UnityEditorPath);
        Assert.Equal(2, config.TomlConfig.Bundles.Count);
        Assert.True(config.TomlConfig.Bundles.ContainsKey("parent_bundle"));
        Assert.True(config.TomlConfig.Bundles.ContainsKey("child_bundle"));
    }

    [Fact]
    public void ExtendedConfig_ShouldLoadDefaultConfigFromDirectory() {
        // Arrange
        string parentDir = Path.Combine(_testConfigDir, "parent");
        Directory.CreateDirectory(parentDir);

        string parentConfigPath = CreateConfigFile(Path.Combine("parent", ".assetbundler.toml"), @"
[global]
unity_version = ""2022.3.35f1""
unity_editor_path = ""/parent/unity""
link_method = ""hardlink""
");

        string childConfigPath = CreateConfigFile("custom.toml", @"
[global]
extends = ""parent""
unity_version = ""2023.1.0f1""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        Assert.Equal("2023.1.0f1", config.TomlConfig.Global.UnityVersion);
        Assert.Equal("/parent/unity", config.TomlConfig.Global.UnityEditorPath);
        Assert.Equal("hardlink", config.TomlConfig.Global.LinkMethod);
    }

    [Fact]
    public void ExtendedConfig_ShouldPrioritizeSameNameOverDefault() {
        // Arrange
        string parentDir = Path.Combine(_testConfigDir, "parent");
        Directory.CreateDirectory(parentDir);

        // Create both same-named and default configs
        string sameNameConfigPath = CreateConfigFile(Path.Combine("parent", "myconfig.toml"), @"
[global]
unity_version = ""2022.3.35f1""
unity_editor_path = ""/samename/unity""
");

        string defaultConfigPath = CreateConfigFile(Path.Combine("parent", ".assetbundler.toml"), @"
[global]
unity_version = ""2021.3.35f1""
unity_editor_path = ""/default/unity""
");

        string childConfigPath = CreateConfigFile("myconfig.toml", @"
[global]
extends = ""parent""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        // Should use the same-named config, not the default
        Assert.Equal("2022.3.35f1", config.TomlConfig.Global.UnityVersion);
        Assert.Equal("/samename/unity", config.TomlConfig.Global.UnityEditorPath);
    }

    [Fact]
    public void ExtendedConfig_ShouldWorkWithParentDirectoryNotation() {
        // Arrange
        string subDir = Path.Combine(_testConfigDir, "child");
        Directory.CreateDirectory(subDir);

        string parentConfigPath = CreateConfigFile(".assetbundler.toml", @"
[global]
unity_version = ""2022.3.35f1""
unity_editor_path = ""/parent/unity""
");

        string childConfigPath = CreateConfigFile(Path.Combine("child", "config.toml"), @"
[global]
extends = ""..""
unity_version = ""2023.1.0f1""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        Assert.Equal("2023.1.0f1", config.TomlConfig.Global.UnityVersion);
        Assert.Equal("/parent/unity", config.TomlConfig.Global.UnityEditorPath);
    }

    [Fact]
    public void ExtendedConfig_ShouldSupportRecursiveDirectoryExtends() {
        // Arrange
        // Create directory structure: grandparent/parent/child
        string grandparentDir = Path.Combine(_testConfigDir, "grandparent");
        string parentDir = Path.Combine(grandparentDir, "parent");
        string childDir = Path.Combine(parentDir, "child");
        Directory.CreateDirectory(grandparentDir);
        Directory.CreateDirectory(parentDir);
        Directory.CreateDirectory(childDir);

        // Grandparent config
        string grandparentConfigPath = CreateConfigFile(Path.Combine("grandparent", ".assetbundler.toml"), @"
[global]
unity_version = ""2021.3.0f1""
unity_editor_path = ""/grandparent/unity""
link_method = ""symlink""

[bundles.grandparent_bundle]
asset_directory = ""/grandparent/assets""
");

        // Parent config extends grandparent
        string parentConfigPath = CreateConfigFile(Path.Combine("grandparent", "parent", ".assetbundler.toml"), @"
[global]
extends = ""..""
unity_version = ""2022.3.0f1""
link_method = ""hardlink""

[bundles.parent_bundle]
asset_directory = ""/parent/assets""
");

        // Child config extends parent
        string childConfigPath = CreateConfigFile(Path.Combine("grandparent", "parent", "child", "config.toml"), @"
[global]
extends = ""..""
unity_version = ""2023.1.0f1""

[bundles.child_bundle]
asset_directory = ""/child/assets""
");

        // Act
        var config = LoadConfigurationFromFile(childConfigPath);

        // Assert
        // Should have values from all three levels
        Assert.Equal("2023.1.0f1", config.TomlConfig.Global.UnityVersion); // From child
        Assert.Equal("/grandparent/unity", config.TomlConfig.Global.UnityEditorPath); // From grandparent
        Assert.Equal("hardlink", config.TomlConfig.Global.LinkMethod); // From parent

        // Should have all three bundles
        Assert.Equal(3, config.TomlConfig.Bundles.Count);
        Assert.True(config.TomlConfig.Bundles.ContainsKey("grandparent_bundle"));
        Assert.True(config.TomlConfig.Bundles.ContainsKey("parent_bundle"));
        Assert.True(config.TomlConfig.Bundles.ContainsKey("child_bundle"));
    }

    private string CreateConfigFile(string filename, string content) {
        string fullPath = Path.Combine(_testConfigDir, filename);
        string directory = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
        _tempFilesToCleanup.Add(fullPath);
        return fullPath;
    }

    private Configuration LoadConfigurationFromFile(string configPath) {
        // Create a minimal command with required arguments
        var rootCommand = new RootCommand();
        var bundleConfigArg = new Argument<string[]>("bundle-config", () => [], "Bundle configurations to build");
        var configOption = new Option<string?>("--config", "Path to configuration file");
        var targetOption = new Option<string[]>("--target", () => [], "Target platforms");
        var quietOption = new Option<bool>("--quiet", "Suppress output");
        var verboseOption = new Option<bool>("--verbose", "Verbose output");
        var debugOption = new Option<bool>("--debug", "Debug output");
        var ciOption = new Option<bool>("--ci", "CI mode");
        var nonInteractiveOption = new Option<bool>("--non-interactive", "Non-interactive mode");
        var listBundlesOption = new Option<bool>("--list-bundles", "List available bundles");
        var dumpConfigOption = new Option<string?>("--dump-config", "Dump configuration");

        rootCommand.AddArgument(bundleConfigArg);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(targetOption);
        rootCommand.AddOption(quietOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(debugOption);
        rootCommand.AddOption(ciOption);
        rootCommand.AddOption(nonInteractiveOption);
        rootCommand.AddOption(listBundlesOption);
        rootCommand.AddOption(dumpConfigOption);

        string[] args = ["--config", configPath, "--ci", "--non-interactive"];
        var parseResult = rootCommand.Parse(args);

        return new Configuration(parseResult);
    }
}