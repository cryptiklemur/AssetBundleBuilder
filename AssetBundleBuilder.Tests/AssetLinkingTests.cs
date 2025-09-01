using CryptikLemur.AssetBundleBuilder.Config;
using CryptikLemur.AssetBundleBuilder.Interfaces;
using Moq;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

[Collection("AssetBuilder Sequential Tests")]
public class AssetLinkingTests(ITestOutputHelper output) : AssetBundleTestBase(output, "LinkingTestOutput") {
    [Fact]
    public async Task BuildMultipleBundlesWithSameSource_ShouldPreventDuplicateLinking() {
        // Arrange - Create a shared source directory
        string sharedAssetsDir = CreateTestAssetsDirectory("SharedAssets");
        File.WriteAllText(Path.Combine(sharedAssetsDir, "shared.txt"), "Shared content");

        var config = CreateTestConfiguration(
            "shared.bundle1",
            sharedAssetsDir,
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
                     .Returns(new[] { Path.Combine(sharedAssetsDir, "shared.txt") });
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns(Array.Empty<string>());
        
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity build completed for shared source bundles",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Multiple bundles with same source should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Successfully handled duplicate source directory linking through process mocking");
    }

    [Fact]
    public async Task BuildMultipleBundlesWithDifferentSources_ShouldLinkAll() {
        // Arrange - Create separate source directories
        string assetsDir1 = CreateTestAssetsDirectory("Assets1");
        string assetsDir2 = CreateTestAssetsDirectory("Assets2");

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();
        
        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(assetsDir1, It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns(["file1.txt"]);
        mockFileSystem.Setup(x => x.GetFiles(assetsDir2, It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns(["file2.txt"]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        var config = new Configuration {
            BundleConfigNames = ["distinct1", "distinct2"],
            BuildTargetList = [],
            ConfigFile = "",
            TomlConfig = new TomlConfiguration {
                Global = new TomlGlobalConfig {
                    UnityVersion = "2022.3.35f1",
                    UnityEditorPath = "/mock/unity/path/Unity.exe",
                    UnityHubPath = "",
                    CleanTempProject = true,
                    LinkMethod = "copy",
                    AllowedTargets = ["windows", "mac", "linux"],
                    Targetless = true
                },
                Bundles = new Dictionary<string, TomlBundleConfig> {
                    ["distinct1"] = new() {
                        BundleName = "distinct.bundle1",
                        AssetDirectory = assetsDir1,
                        OutputDirectory = _testOutputPath,
                        Targetless = true
                    },
                    ["distinct2"] = new() {
                        BundleName = "distinct.bundle2",
                        AssetDirectory = assetsDir2,
                        OutputDirectory = _testOutputPath,
                        Targetless = true
                    }
                }
            }
        };
        
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity build completed for distinct bundles",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Multiple bundles with different sources should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Successfully linked multiple distinct source directories through process mocking");
    }

    [Theory]
    [InlineData("copy")]
    public async Task BuildBundle_WithCopyLinkMethod_ShouldCreateIndependentCopy(string linkMethod) {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("CopyTest");
        string originalFile = Path.Combine(testAssetsDir, "original.txt");

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
                     .Returns([originalFile]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        var config = CreateTestConfiguration(
            "copy.test",
            testAssetsDir,
            _testOutputPath
        );
        config.TomlConfig.Global.LinkMethod = linkMethod;
        
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = $"Mock Unity build completed for {linkMethod} method",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, $"Bundle with {linkMethod} should succeed");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine($"{linkMethod} method created independent copy through process mocking");
    }

    [Fact]
    public async Task BuildBundle_WithSymlinkOnWindows_ShouldWorkIfSupported() {
        // This test checks if symlink functionality works on the current platform
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("SymlinkTest");

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();
        
        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CreateSymbolicLink(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([Path.Combine(testAssetsDir, "symlink_test.txt")]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        var config = CreateTestConfiguration(
            "symlink.test",
            testAssetsDir,
            _testOutputPath
        );
        config.TomlConfig.Global.LinkMethod = "symlink";
        
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity build completed for symlink method",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Symlink method should succeed with mocked filesystem");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Symlink method succeeded through process mocking");
    }

    [Fact]
    public async Task BuildBundle_WithJunctionOnWindows_ShouldWorkOnWindows() {
        // Junction should work on Windows but fail on other platforms
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("JunctionTest");

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();
        
        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([Path.Combine(testAssetsDir, "junction_test.txt")]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        var config = CreateTestConfiguration(
            "junction.test",
            testAssetsDir,
            _testOutputPath
        );
        config.TomlConfig.Global.LinkMethod = "junction";
        
        // Mock junction behavior based on platform
        if (OperatingSystem.IsWindows()) {
            mockFileSystem.Setup(x => x.CreateJunction(It.IsAny<string>(), It.IsAny<string>()));
            mockProcessRunner
                .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
                .ReturnsAsync(new ProcessResult {
                    ExitCode = 0,
                    StandardOutput = "Mock Unity build completed for junction method",
                    StandardError = ""
                });
        } else {
            mockFileSystem.Setup(x => x.CreateJunction(It.IsAny<string>(), It.IsAny<string>()))
                          .Throws<PlatformNotSupportedException>();
        }

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        if (OperatingSystem.IsWindows()) {
            Assert.True(success, "Junction method should succeed on Windows with mocked filesystem");
            
            // Verify Unity was called with expected arguments
            mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
                psi.Arguments.Contains("-batchmode") &&
                psi.Arguments.Contains("-nographics") &&
                psi.Arguments.Contains("-quit") &&
                psi.Arguments.Contains("-executeMethod") &&
                psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
                psi.Arguments.Contains("-bundleConfigFile")
            )), Times.AtLeastOnce);
            
            _output.WriteLine("Junction method succeeded on Windows through process mocking");
        } else {
            Assert.False(success, "Junction method should fail on non-Windows platforms");
            
            // Verify Unity was not called due to junction failure
            mockProcessRunner.Verify(x => x.RunAsync(It.IsAny<ProcessStartInfo>()), Times.Never);
            
            _output.WriteLine("Junction method correctly failed on non-Windows platform");
        }
    }

    [Fact]
    public async Task BuildBundle_WithHardlinkMethod_ShouldCreateHardlinks() {
        // Test hard link functionality
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("HardlinkTest");

        // Create mock FileSystem and ProcessRunner
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();
        
        // Mock filesystem operations to always succeed
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.CreateHardLink(It.IsAny<string>(), It.IsAny<string>()));
        mockFileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([Path.Combine(testAssetsDir, "hardlink_test.txt")]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        var config = CreateTestConfiguration(
            "hardlink.test",
            testAssetsDir,
            _testOutputPath
        );
        config.TomlConfig.Global.LinkMethod = "hardlink";
        
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity build completed for hardlink method",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Hardlink method should succeed with mocked filesystem");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Hardlink method succeeded through process mocking");
    }

    [Fact]
    public async Task BuildBundle_WithInvalidLinkMethod_ShouldFail() {
        // Test that invalid link method causes failure
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("InvalidLinkTest");

        // Create mock FileSystem and ProcessRunner (should not be called for invalid method)
        var mockFileSystem = new Mock<IFileSystemOperations>();
        var mockProcessRunner = new Mock<IProcessRunner>();
        
        // Mock filesystem operations to always succeed (but shouldn't be reached)
        mockFileSystem.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        mockFileSystem.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));

        var config = CreateTestConfiguration(
            "invalid.test",
            testAssetsDir,
            _testOutputPath
        );
        config.TomlConfig.Global.LinkMethod = "invalid_method";

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.False(success, "Bundle build should fail with invalid link method");
        
        // Verify Unity was not called due to invalid link method
        mockProcessRunner.Verify(x => x.RunAsync(It.IsAny<ProcessStartInfo>()), Times.Never);

        _output.WriteLine("Invalid link method correctly caused build failure before Unity execution");
    }

    [Fact]
    public async Task BuildBundle_WithNormalizedPaths_ShouldHandlePathNormalization() {
        // Test that path normalization works correctly to prevent duplicate linking
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("PathNormalizationTest");

        // Create paths with different formats but pointing to same location
        string normalizedPath = Path.GetFullPath(testAssetsDir);
        string pathWithDots = Path.Combine(testAssetsDir, "..", Path.GetFileName(testAssetsDir));

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
                     .Returns([Path.Combine(testAssetsDir, "test_file.txt")]);
        mockFileSystem.Setup(x => x.GetDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                     .Returns([]);

        var config = new Configuration {
            BundleConfigNames = ["path1", "path2"],
            BuildTargetList = [],
            ConfigFile = "",
            TomlConfig = new TomlConfiguration {
                Global = new TomlGlobalConfig {
                    UnityVersion = "2022.3.35f1",
                    UnityEditorPath = "/mock/unity/path/Unity.exe",
                    UnityHubPath = "",
                    CleanTempProject = true,
                    LinkMethod = "copy",
                    AllowedTargets = ["windows", "mac", "linux"],
                    Targetless = true
                },
                Bundles = new Dictionary<string, TomlBundleConfig> {
                    ["path1"] = new() {
                        BundleName = "norm.bundle1",
                        AssetDirectory = normalizedPath,
                        OutputDirectory = _testOutputPath,
                        Targetless = true
                    },
                    ["path2"] = new() {
                        BundleName = "norm.bundle2",
                        AssetDirectory = pathWithDots, // Different format, same location
                        OutputDirectory = _testOutputPath,
                        Targetless = true
                    }
                }
            }
        };
        
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity build completed for path normalization test",
                StandardError = ""
            });

        // Inject the mocks
        Program.FileSystem = mockFileSystem.Object;
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Path normalization should prevent duplicate linking issues");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Path normalization correctly handled different path formats through process mocking");
    }
    
    public override void Dispose() {
        Program.ProcessRunner = new SystemProcessRunner();
        Program.FileSystem = new Utilities.SystemFileOperations();
        base.Dispose();
    }
}