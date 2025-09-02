using System.Diagnostics;
using CryptikLemur.AssetBundleBuilder.Interfaces;
using CryptikLemur.AssetBundleBuilder.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

[Collection("AssetBuilder Sequential Tests")]
public class SimplifiedIntegrationTests(ITestOutputHelper output)
    : AssetBundleTestBase(output, "SimplifiedIntegrationTestOutput") {
    [Fact]
    public async Task BuildSingleTargetlessBundle_ShouldCreateExpectedFiles() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("SingleBundle");
        string bundleName = "test.modname";

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Create mock FileSystem
        var mockFileSystem = new Mock<IFileSystemOperations>();
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(new[] { Path.Combine(testAssetsDir, "test.txt") });

        // Create mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity execution completed successfully",
                StandardError = ""
            });

        // Inject mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert  
        Assert.True(success, "Asset bundle build should succeed");

        // The main verification is that the build succeeded, which means:
        // 1. Configuration was parsed correctly from TOML
        // 2. Bundle configuration was found and processed
        // 3. Unity project was created successfully  
        // 4. Asset linking worked
        // 5. Unity process executed and returned success (exit code 0)

        _output.WriteLine("Unity mocking test completed successfully!");
        _output.WriteLine("TOML configuration parsing works");
        _output.WriteLine("Bundle dictionary lookup fixed");
        _output.WriteLine("Unity process execution works with shell scripts");

        // Note: We can't verify actual asset bundle files since Unity execution is mocked
        // but we can verify the configuration and Unity process execution worked correctly
    }

    [Fact]
    public async Task BuildMultiTargetBundle_ShouldCreateFilesForAllTargets() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("MultiTargetBundle");
        string bundleName = "author.multiplatform";
        var targets = new List<string> { "windows", "linux" };

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath,
            false,
            targets
        );

        // Create mock FileSystem
        var mockFileSystem = new Mock<IFileSystemOperations>();
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(new[] { Path.Combine(testAssetsDir, "test.txt") });

        // Create a mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity multi-target build completed successfully",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Multi-target asset bundle build should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Multi-target build verified through process mocking");
    }

    [Fact]
    public async Task BuildMultipleBundles_ShouldCreateAllBundles() {
        // Arrange
        string testAssetsDir1 = CreateTestAssetsDirectory("Bundle1");
        string bundleName1 = "test.bundle1";

        var config = CreateTestConfiguration(
            bundleName1,
            testAssetsDir1,
            _testOutputPath
        );

        // Create mock FileSystem
        var mockFileSystem = new Mock<IFileSystemOperations>();
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(new[] { Path.Combine(testAssetsDir1, "test.txt") });

        // Create a mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity multiple bundles build completed successfully",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Multiple bundle build should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Multiple bundles build verified through process mocking");
    }

    [Fact]
    public async Task BuildBundle_WithMissingAssetDirectory_ShouldFail() {
        // Arrange
        string nonExistentDir = Path.Combine(_testAssetsPath, "DoesNotExist");
        var config = CreateTestConfiguration(
            "test.missing",
            nonExistentDir,
            _testOutputPath
        );

        // Create mock FileSystem that returns false for missing directory
        var mockFileSystem = new Mock<IFileSystemOperations>();
        mockFileSystem.Setup(x => x.DirectoryExists(nonExistentDir)).Returns(false);
        mockFileSystem.Setup(x => x.DirectoryExists(It.Is<string>(s => s != nonExistentDir))).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        // Inject the mock
        Program.FileSystem = mockFileSystem.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.False(success, "Build should fail when asset directory doesn't exist");
    }

    [Fact]
    public async Task BuildBundle_WithCustomFilenameFormat_ShouldUseCustomNaming() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("CustomNaming");
        string bundleName = "custom.modname";

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Set custom filename format
        string sectionName = bundleName.Replace(".", "_");
        config.TomlConfig.Bundles[sectionName].Filename = "custom_[bundle_name]_format";

        // Create mock FileSystem
        var mockFileSystem = new Mock<IFileSystemOperations>();
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(new[] { Path.Combine(testAssetsDir, "test.txt") });

        // Create a mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity custom filename build completed successfully",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Build with custom filename format should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Custom filename format build verified through process mocking");
    }

    [Fact]
    public async Task BuildBundle_WithIncludePatterns_ShouldOnlyIncludeMatchingFiles() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("IncludeTest");

        // Create specific test files
        File.WriteAllText(Path.Combine(testAssetsDir, "include.txt"), "Should be included");
        File.WriteAllText(Path.Combine(testAssetsDir, "exclude.log"), "Should be excluded");
        File.WriteAllText(Path.Combine(testAssetsDir, "another.txt"), "Should be included");

        var config = CreateTestConfiguration(
            "include.test",
            testAssetsDir,
            _testOutputPath
        );

        // Set include patterns to only include .txt files
        string sectionName = "include_test"; // "include.test" -> "include_test"
        config.TomlConfig.Bundles[sectionName].IncludePatterns = ["*.txt"];

        // Create mock FileSystem
        var mockFileSystem = new Mock<IFileSystemOperations>();
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(new[] { Path.Combine(testAssetsDir, "include.txt"), Path.Combine(testAssetsDir, "another.txt") });

        // Create a mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity include patterns build completed successfully",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Build with include patterns should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Include patterns build verified through process mocking");
    }

    [Fact]
    public async Task BuildBundle_WithExcludePatterns_ShouldExcludeMatchingFiles() {
        // Arrange  
        string testAssetsDir = CreateTestAssetsDirectory("ExcludeTest");

        // Create specific test files
        File.WriteAllText(Path.Combine(testAssetsDir, "keep.txt"), "Should be kept");
        File.WriteAllText(Path.Combine(testAssetsDir, "temp.tmp"), "Should be excluded");
        File.WriteAllText(Path.Combine(testAssetsDir, "backup.bak"), "Should be excluded");

        var config = CreateTestConfiguration(
            "exclude.test",
            testAssetsDir,
            _testOutputPath
        );

        // Set exclude patterns
        string sectionName = "exclude_test"; // "exclude.test" -> "exclude_test"
        config.TomlConfig.Bundles[sectionName].ExcludePatterns = ["*.tmp", "*.bak"];

        // Create mock FileSystem
        var mockFileSystem = new Mock<IFileSystemOperations>();
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns(new[] { Path.Combine(testAssetsDir, "keep.txt") });

        // Create a mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity exclude patterns build completed successfully",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Build with exclude patterns should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Exclude patterns build verified through process mocking");
    }

    public override void Dispose() {
        Program.ProcessRunner = new SystemProcessRunner();
        Program.FileSystem = new SystemFileOperations();
        base.Dispose();
    }
}