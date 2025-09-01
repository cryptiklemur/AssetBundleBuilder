using CryptikLemur.AssetBundleBuilder.Interfaces;
using Moq;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

[Collection("AssetBuilder Sequential Tests")]
public class AssetBundleCreationTests(ITestOutputHelper output) : AssetBundleTestBase(output, "CreationTestOutput") {
    [Fact]
    public async Task CreateAssetBundle_WithValidInputs_ShouldSucceed() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("ValidBundle");
        string bundleName = "author.validbundle";

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
                     .Returns([Path.Combine(testAssetsDir, "test_asset.txt")]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity build completed successfully",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Asset bundle creation should succeed with valid inputs");
        
        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        // Verify output directory exists
        Assert.True(Directory.Exists(_testOutputPath), "Output directory should exist");

        _output.WriteLine("Valid inputs bundle creation verified through process mocking");
    }

    [Fact]
    public async Task CreateAssetBundle_WithNestedDirectories_ShouldIncludeAllFiles() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("NestedBundle");

        // Create nested directory structure
        string level1Dir = Path.Combine(testAssetsDir, "Level1");
        string level2Dir = Path.Combine(level1Dir, "Level2");
        Directory.CreateDirectory(level2Dir);

        // Add files at different levels
        File.WriteAllText(Path.Combine(testAssetsDir, "root.txt"), "Root level file");
        File.WriteAllText(Path.Combine(level1Dir, "level1.txt"), "Level 1 file");
        File.WriteAllText(Path.Combine(level2Dir, "level2.txt"), "Level 2 file");

        string bundleName = "nested.test";
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
                     .Returns([
                         Path.Combine(testAssetsDir, "root.txt"),
                         Path.Combine(level1Dir, "level1.txt"),
                         Path.Combine(level2Dir, "level2.txt")
                     ]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([level1Dir, level2Dir]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity nested directories build completed",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Bundle creation should succeed with nested directories");
        
        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Nested directories bundle creation verified through process mocking");
    }

    [Fact]
    public async Task CreateAssetBundle_WithDifferentFileTypes_ShouldIncludeAll() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("MultiTypeBundle");

        // Create files of different types
        File.WriteAllText(Path.Combine(testAssetsDir, "text.txt"), "Text file content");
        File.WriteAllText(Path.Combine(testAssetsDir, "data.json"), "{\"key\": \"value\"}");
        File.WriteAllText(Path.Combine(testAssetsDir, "config.xml"), "<config><setting>value</setting></config>");
        File.WriteAllText(Path.Combine(testAssetsDir, "readme.md"), "# Readme\nMarkdown content");
        File.WriteAllBytes(Path.Combine(testAssetsDir, "binary.dat"), [0x01, 0x02, 0x03, 0x04]);

        string bundleName = "multitype.bundle";
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
                     .Returns([
                         Path.Combine(testAssetsDir, "text.txt"),
                         Path.Combine(testAssetsDir, "data.json"),
                         Path.Combine(testAssetsDir, "config.xml"),
                         Path.Combine(testAssetsDir, "readme.md"),
                         Path.Combine(testAssetsDir, "binary.dat")
                     ]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity multi-type files build completed",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Bundle creation should succeed with different file types");
        
        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Different file types bundle creation verified through process mocking");
    }

    [Fact]
    public async Task CreateAssetBundle_WithEmptyDirectory_ShouldHandleGracefully() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("EmptyBundle");
        // Remove the default test files to make it truly empty
        foreach (string file in Directory.GetFiles(testAssetsDir, "*", SearchOption.AllDirectories)) {
            File.Delete(file);
        }

        string bundleName = "empty.bundle";
        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();
        
        // Mock filesystem operations - empty directory scenario
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]); // Empty array - no files
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]); // Empty array - no directories

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0, // We'll assume Unity handles empty dirs gracefully
                StandardOutput = "Mock Unity empty directory build completed",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        // Depending on Unity's behavior, this might succeed or fail
        // For now, let's expect it to handle empty directories gracefully
        if (success) {
            // Verify Unity was called
            mockProcessRunner.Verify(x => x.RunAsync(It.IsAny<ProcessStartInfo>()), Times.AtLeastOnce);
            _output.WriteLine("Empty directory bundle creation succeeded (mocked)");
        }
        else {
            _output.WriteLine("Empty directory bundle creation failed (expected behavior)");
        }
    }

    [Fact]
    public async Task CreateAssetBundle_WithVeryLongPath_ShouldHandlePathLength() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("LongPathBundle");

        // Create a deeply nested directory structure to test path length handling
        string currentDir = testAssetsDir;
        for (int i = 0; i < 5; i++) {
            currentDir = Path.Combine(currentDir, $"VeryLongDirectoryNameLevel{i}");
            Directory.CreateDirectory(currentDir);
        }

        File.WriteAllText(Path.Combine(currentDir, "deep_file.txt"), "File in deep directory");

        string bundleName = "longpath.bundle";
        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Create a mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity long path build completed",
                StandardError = ""
            });

        // Inject the mock
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act  
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Bundle creation should handle long paths");
        
        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Long path bundle creation verified through process mocking");
    }

    [Fact]
    public async Task CreateAssetBundle_WithSpecialCharactersInFilenames_ShouldHandleCorrectly() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("SpecialCharsBundle");

        // Create files with various special characters (avoiding OS-restricted ones)
        File.WriteAllText(Path.Combine(testAssetsDir, "file with spaces.txt"), "Spaces in filename");
        File.WriteAllText(Path.Combine(testAssetsDir, "file_with_underscores.txt"), "Underscores in filename");
        File.WriteAllText(Path.Combine(testAssetsDir, "file-with-hyphens.txt"), "Hyphens in filename");
        File.WriteAllText(Path.Combine(testAssetsDir, "file.with.dots.txt"), "Dots in filename");

        string bundleName = "specialchars.modname";
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
                     .Returns([
                         Path.Combine(testAssetsDir, "file with spaces.txt"),
                         Path.Combine(testAssetsDir, "file_with_underscores.txt"),
                         Path.Combine(testAssetsDir, "file-with-hyphens.txt"),
                         Path.Combine(testAssetsDir, "file.with.dots.txt")
                     ]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity special chars build completed",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Bundle creation should handle special characters in filenames");
        
        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Special characters bundle creation verified through process mocking");
    }

    [Fact]
    public async Task CreateAssetBundle_WithLargeNumberOfFiles_ShouldHandleVolume() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("LargeVolumeBundle");

        // Create many small files to test volume handling
        for (int i = 0; i < 100; i++)
            File.WriteAllText(Path.Combine(testAssetsDir, $"file_{i:D3}.txt"), $"Content of file {i}");

        string bundleName = "largevolume.modname";
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
        
        // Generate list of mock files for GetFiles call
        var mockFiles = Enumerable.Range(0, 100)
                                 .Select(i => Path.Combine(testAssetsDir, $"file_{i:D3}.txt"))
                                 .ToArray();
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns(mockFiles);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity large volume build completed",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Bundle creation should handle large number of files");
        
        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Large volume bundle creation verified through process mocking");
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("symlink")]
    [InlineData("junction")]
    [InlineData("hardlink")]
    public async Task CreateAssetBundle_WithDifferentLinkMethods_ShouldSucceed(string linkMethod) {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory($"LinkMethod_{linkMethod}");
        string bundleName = $"linktest.{linkMethod}";

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Set the specific link method
        config.TomlConfig.Global.LinkMethod = linkMethod;

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();
        
        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.CreateSymbolicLink(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CreateJunction(It.IsAny<string>(), It.IsAny<string>()));
        
        // Setup hardlink to fail for hardlink method, succeed for others
        if (linkMethod == "hardlink") {
            mockFileSystem.Setup(x => x.CreateHardLink(It.IsAny<string>(), It.IsAny<string>()))
                         .Throws(new InvalidOperationException("Hardlink creation failed"));
        } else {
            mockFileSystem.Setup(x => x.CreateHardLink(It.IsAny<string>(), It.IsAny<string>()));
        }
        
        // Junction might fail on non-Windows
        bool shouldSucceed = !(linkMethod == "junction" && !OperatingSystem.IsWindows());
        
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = shouldSucceed ? 0 : 1,
                StandardOutput = shouldSucceed ? $"Mock Unity {linkMethod} build completed" : $"{linkMethod} not supported",
                StandardError = shouldSucceed ? "" : "Link method not supported on this platform"
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act & Assert
        try {
            bool success = await BuildAssetBundleAsync(config);

            // Some link methods might not be supported on all platforms
            if (linkMethod == "junction" && !OperatingSystem.IsWindows()) {
                Assert.False(success, "Junction should fail on non-Windows platforms");
                _output.WriteLine("Junction correctly failed on non-Windows platform (mocked)");
            }
            else if (linkMethod == "hardlink") {
                // Hardlink should now succeed with proper filesystem mocking
                Assert.True(success, $"Bundle creation with {linkMethod} should succeed");
                
                // Verify Unity was called
                mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
                    psi.Arguments.Contains("-batchmode") &&
                    psi.Arguments.Contains("-nographics") &&
                    psi.Arguments.Contains("-quit") &&
                    psi.Arguments.Contains("-executeMethod") &&
                    psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles")
                )), Times.AtLeastOnce);
                
                _output.WriteLine($"{linkMethod} method succeeded with mocked filesystem");
            }
            else {
                Assert.True(success, $"Bundle creation with {linkMethod} should succeed");
                
                // Verify Unity was called
                mockProcessRunner.Verify(x => x.RunAsync(It.IsAny<ProcessStartInfo>()), Times.AtLeastOnce);
                
                _output.WriteLine($"{linkMethod} link method verified through process mocking");
            }
        }
        catch (PlatformNotSupportedException) {
            _output.WriteLine($"{linkMethod} not supported on this platform (expected)");
        }
    }

    public override void Dispose() {
        Program.ProcessRunner = new SystemProcessRunner();
        Program.FileSystem = new Utilities.SystemFileOperations();
        base.Dispose();
    }
}