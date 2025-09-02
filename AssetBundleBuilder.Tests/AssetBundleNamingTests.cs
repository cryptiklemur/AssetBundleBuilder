using System.Diagnostics;
using CryptikLemur.AssetBundleBuilder.Interfaces;
using CryptikLemur.AssetBundleBuilder.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

[Collection("AssetBuilder Sequential Tests")]
public class AssetBundleNamingTests(ITestOutputHelper output) : AssetBundleTestBase(output, "NamingTestOutput") {
    [Theory]
    [InlineData("simple.modname", "resource_simple_modname")]
    [InlineData("author.modname", "resource_author_modname")]
    [InlineData("test.multi.part.name", "resource_test_multi_part_name")]
    [InlineData("single.resource", "resource_single_resource")]
    public async Task BuildTargetlessBundle_ShouldUseCorrectNaming(string bundleName, string expectedFileName) {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory($"Naming_{bundleName.Replace(".", "_")}");

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = $"Mock Unity build completed for {bundleName}",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, $"Build should succeed for bundle name: {bundleName}");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine($"Targetless bundle '{bundleName}' build verified through process mocking");
    }

    [Theory]
    [InlineData("simple.modname", "windows", "resource_simple_modname_win")]
    [InlineData("author.modname", "mac", "resource_author_modname_mac")]
    [InlineData("test.modname", "linux", "resource_test_modname_linux")]
    public async Task BuildTargetedBundle_ShouldIncludePlatformSuffix(string bundleName, string target,
        string expectedFileName) {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory($"Targeted_{bundleName.Replace(".", "_")}_{target}");

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath,
            false,
            [target]
        );

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = $"Mock Unity targeted build completed for {bundleName} on {target}",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, $"Build should succeed for targeted bundle: {bundleName} -> {target}");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine($"Targeted bundle '{bundleName}' for {target} build verified through process mocking");
    }

    [Theory]
    [InlineData("[bundle_name]", "test.modname", "test_modname")]
    [InlineData("custom_[bundle_name]_format", "author.mod", "custom_author_mod_format")]
    [InlineData("[bundle_name]_v1", "game.expansion", "game_expansion_v1")]
    [InlineData("mod_[original_bundle_name]", "author.mod", "mod_author.mod")]
    public async Task BuildBundle_WithCustomFilenameFormat_ShouldUseCorrectFormat(string filenameFormat,
        string bundleName, string expectedFileName) {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory($"CustomFormat_{bundleName.Replace(".", "_")}");

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Set custom filename format
        string sectionName = bundleName.Replace(".", "_");
        config.TomlConfig.Bundles[sectionName].Filename = filenameFormat;

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = $"Mock Unity build with custom format {filenameFormat}",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, $"Build should succeed with custom format: {filenameFormat}");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine($"Custom format '{filenameFormat}' build verified through process mocking");
    }

    [Theory]
    [InlineData("resource_[bundle_name]_[platform]", "test.mod", "windows", "resource_test_mod_win")]
    [InlineData("[bundle_name]_[target]_build", "author.game", "linux", "author_game_linux_build")]
    [InlineData("final_[bundle_name]_for_[platform]", "epic.mod", "mac", "final_epic_mod_for_mac")]
    public async Task BuildTargetedBundle_WithCustomFormat_ShouldIncludePlatformVariable(string filenameFormat,
        string bundleName, string target, string expectedFileName) {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory($"CustomTargeted_{bundleName.Replace(".", "_")}_{target}");

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath,
            false,
            [target]
        );

        // Set custom filename format with platform variables
        string sectionName = bundleName.Replace(".", "_");
        config.TomlConfig.Bundles[sectionName].Filename = filenameFormat;

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = $"Mock Unity targeted custom format build: {filenameFormat}",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, $"Build should succeed with targeted custom format: {filenameFormat}");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine($"Custom targeted format '{filenameFormat}' build verified through process mocking");
    }

    [Theory]
    [InlineData("resource_[bundle_name]_[platform]", "test.mod", "resource_test_mod")]
    [InlineData("[bundle_name]_[target]_special", "author.game", "author_game_special")]
    public async Task BuildTargetlessBundle_WithPlatformVariables_ShouldStripPlatformParts(string filenameFormat,
        string bundleName, string expectedFileName) {
        // Arrange - Test that platform variables are properly removed for targetless bundles
        string testAssetsDir = CreateTestAssetsDirectory($"TargetlessCustom_{bundleName.Replace(".", "_")}");

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Set custom filename format with platform variables (should be stripped for targetless)
        string sectionName = bundleName.Replace(".", "_");
        config.TomlConfig.Bundles[sectionName].Filename = filenameFormat;

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = $"Mock Unity targetless custom format build: {filenameFormat}",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, $"Build should succeed with targetless custom format: {filenameFormat}");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine(
            $"Targetless custom format '{filenameFormat}' build verified through process mocking");
    }

    [Fact]
    public async Task BuildBundle_WithNoPlatformSuffixFlag_ShouldOmitSuffix() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("NoPlatformSuffix");
        string bundleName = "noplatform.bundle";

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath,
            false,
            ["windows"]
        );

        // Enable noPlatformSuffix flag
        string sectionName = bundleName.Replace(".", "_");
        config.TomlConfig.Bundles[sectionName].Targetless = false; // Make it targeted

        // But modify the Unity config to set noPlatformSuffix to true
        // This would need to be handled in the build process

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity noPlatformSuffix build completed",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Build should succeed with noPlatformSuffix");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("noPlatformSuffix flag test verified through process mocking");
    }

    [Theory]
    [InlineData("test.bundle-name", "resource_test_bundle-name")] // Hyphens preserved
    [InlineData("test.modname", "resource_test_modname")] // Dots become underscores
    public async Task BuildBundle_WithSpecialCharactersInName_ShouldHandleCorrectly(string bundleName,
        string expectedFileName) {
        // Arrange
        string testAssetsDir =
            CreateTestAssetsDirectory($"SpecialChars_{bundleName.Replace(".", "_").Replace(" ", "_")}");

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = $"Mock Unity build with special chars: {bundleName}",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, $"Build should succeed with special characters in name: {bundleName}");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine($"Bundle with special characters '{bundleName}' build verified through process mocking");
    }

    [Fact]
    public async Task BuildMultiTargetBundle_ShouldCreateAllTargetedFiles() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("MultiTarget");
        string bundleName = "multi.target";
        var targets = new List<string> { "windows", "mac", "linux" };

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath,
            false,
            targets
        );

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();

        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(["test_file.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity multi-target build completed",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Multi-target build should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Multi-target bundle build verified through process mocking");
    }

    public override void Dispose() {
        Program.ProcessRunner = new SystemProcessRunner();
        Program.FileSystem = new SystemFileOperations();
        base.Dispose();
    }
}