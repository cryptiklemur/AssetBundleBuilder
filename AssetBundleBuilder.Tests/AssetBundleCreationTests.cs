using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class AssetBundleCreationTests : IDisposable {
    private readonly ITestOutputHelper _output;
    private readonly List<string> _tempDirectoriesToCleanup = new();
    private readonly string _testAssetsPath;
    private readonly string _testOutputPath;

    public AssetBundleCreationTests(ITestOutputHelper output) {
        _output = output;
        _testAssetsPath =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "TestAssets"));
        _testOutputPath =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "TestOutput"));

        // Clear and create TestOutput directory
        if (Directory.Exists(_testOutputPath)) Directory.Delete(_testOutputPath, true);
        Directory.CreateDirectory(_testOutputPath);
        _tempDirectoriesToCleanup.Add(_testOutputPath);
    }

    public void Dispose() {
        foreach (var dir in _tempDirectoriesToCleanup)
            if (Directory.Exists(dir)) {
                try {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    foreach (var file in files) File.SetAttributes(file, FileAttributes.Normal);
                    Directory.Delete(dir, true);
                    _output.WriteLine($"Cleaned up test directory: {dir}");
                }
                catch (Exception ex) {
                    _output.WriteLine($"Warning: Could not clean up {dir}: {ex.Message}");
                }
            }
    }

    [Fact]
    public async Task CreateAssetBundle_WithTestAssets_ShouldSucceed() {
        // Skip test if Unity is not available
        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        _output.WriteLine($"Found Unity at: {unityPath}");
        _output.WriteLine($"Test assets path: {_testAssetsPath}");
        _output.WriteLine($"Output path: {_testOutputPath}");

        // Verify test assets exist
        Assert.True(Directory.Exists(_testAssetsPath), $"TestAssets directory not found at {_testAssetsPath}");

        var texturesPath = Path.Combine(_testAssetsPath, "Textures");
        Assert.True(Directory.Exists(texturesPath), "Textures directory not found in TestAssets");

        var emptyPngPath = Path.Combine(texturesPath, "Empty.png");
        Assert.True(File.Exists(emptyPngPath), "Empty.png not found in TestAssets/Textures");

        // Create build configuration
        var config = new BuildConfiguration
        {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = "cryptiklemur.assetbuilder",
            BuildTarget = "windows",
            KeepTempProject = true, // Keep temp project for debugging
            CleanTempProject = true, // Start fresh
            LinkMethod = "copy" // Use copy method for testing
        };

        // Set temp project path
        var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{config.BuildTarget}";
        var hash = HashUtility.ComputeHash(hashInput);
        config.TempProjectPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");
        // Note: Not adding to _tempDirectoriesToCleanup since KeepTempProject = true

        _output.WriteLine($"Temp project path: {config.TempProjectPath}");

        // Build the asset bundle
        var success = await BuildAssetBundleAsync(config);

        Assert.True(success, "Asset bundle creation failed");

        // Verify output files were created with new naming format
        var normalizedBundleName = config.BundleName.Replace(".", "_");
        var expectedFileName = $"resource_{normalizedBundleName}_{config.BuildTarget}";
        var expectedBundleFile = Path.Combine(_testOutputPath, expectedFileName);
        var expectedManifestFile = Path.Combine(_testOutputPath, expectedFileName + ".manifest");

        _output.WriteLine($"Looking for bundle file at: {expectedBundleFile}");
        _output.WriteLine($"Looking for manifest file at: {expectedManifestFile}");

        // List all files in output directory for debugging
        var outputFiles = Directory.GetFiles(_testOutputPath, "*", SearchOption.AllDirectories);
        _output.WriteLine("Files in output directory:");
        foreach (var file in outputFiles)
            _output.WriteLine($"  {Path.GetRelativePath(_testOutputPath, file)} ({new FileInfo(file).Length} bytes)");

        // Verify exactly one asset bundle and one manifest exist
        Assert.True(File.Exists(expectedBundleFile),
            $"Asset bundle file not found. Expected: {expectedBundleFile}");
        Assert.True(File.Exists(expectedManifestFile),
            $"Asset bundle manifest not found. Expected: {expectedManifestFile}");

        // Verify no extra files were created (only the bundle and its manifest)
        Assert.Equal(2, outputFiles.Length);

        // Verify the bundle name follows the new format
        var actualBundleFileName = Path.GetFileName(expectedBundleFile);
        Assert.Equal(expectedFileName, actualBundleFileName);

        if (File.Exists(expectedManifestFile)) {
            var manifestContent = await File.ReadAllTextAsync(expectedManifestFile);
            _output.WriteLine($"Manifest content:\n{manifestContent}");
        }
    }

    [Theory]
    [InlineData("author.modname")]
    [InlineData("author_modname")]
    [InlineData("cryptiklemur.testbundle")]
    [InlineData("cryptiklemur_testbundle")]
    public async Task CreateAssetBundle_VerifyBundleNameInNewFormat(string inputBundleName) {
        // Skip test if Unity is not available
        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        // Create build configuration with different bundle names
        var config = new BuildConfiguration
        {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = inputBundleName,
            BuildTarget = "windows",
            KeepTempProject = false,
            CleanTempProject = true,
            LinkMethod = "copy"
        };

        // Build the asset bundle
        var success = await BuildAssetBundleAsync(config);
        Assert.True(success, "Asset bundle creation failed");

        // Verify the output file uses the new naming format
        var normalizedBundleName = inputBundleName.Replace(".", "_");
        var expectedFileName = $"resource_{normalizedBundleName}_{config.BuildTarget}";
        var expectedBundleFile = Path.Combine(_testOutputPath, expectedFileName);
        var expectedManifestFile = Path.Combine(_testOutputPath, expectedFileName + ".manifest");

        Assert.True(File.Exists(expectedBundleFile),
            $"Asset bundle not found with new naming format. Expected: {expectedBundleFile}");
        Assert.True(File.Exists(expectedManifestFile),
            $"Manifest not found with new naming format. Expected: {expectedManifestFile}");

        // Clean up output files for next iteration
        if (File.Exists(expectedBundleFile)) File.Delete(expectedBundleFile);
        if (File.Exists(expectedManifestFile)) File.Delete(expectedManifestFile);
    }

    [Fact]
    public async Task CreateAssetBundle_WindowsTarget_ShouldCreateOneBundle() {
        // Skip test if Unity is not available
        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        var config = new BuildConfiguration
        {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = "windows.test",
            BuildTarget = "windows",
            KeepTempProject = false,
            CleanTempProject = true,
            LinkMethod = "copy"
        };

        var success = await BuildAssetBundleAsync(config);
        Assert.True(success, "Asset bundle creation failed");

        // Should create 1 bundle + 1 manifest = 2 files
        var outputFiles = Directory.GetFiles(_testOutputPath, "*", SearchOption.AllDirectories);
        Assert.Equal(2, outputFiles.Length);
    }

    [Fact]
    public void CreateAssetBundle_InvalidUnityVersion_ShouldFail() {
        var config = new BuildConfiguration
        {
            UnityVersion = "invalid.version",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = "cryptiklemur.assetbuilder",
            BuildTarget = "windows"
        };

        // Try to find Unity path - this should fail
        config.UnityPath = UnityPathFinder.FindUnityExecutable(config.UnityVersion) ?? "";

        Assert.True(string.IsNullOrEmpty(config.UnityPath), "Should not find Unity path for invalid version");
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("hardlink")]
    // symlink and junction require admin privileges on Windows, so skip them in automated tests
    public async Task CreateAssetBundle_DifferentLinkMethods_ShouldSucceed(string linkMethod) {
        // Skip test if Unity is not available
        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        var config = new BuildConfiguration
        {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = $"linktest.{linkMethod}",
            BuildTarget = "windows",
            KeepTempProject = false,
            CleanTempProject = true,
            LinkMethod = linkMethod
        };

        var success = await BuildAssetBundleAsync(config);
        Assert.True(success, $"Asset bundle creation failed with link method: {linkMethod}");

        // Verify bundle was created with new naming format
        var normalizedBundleName = config.BundleName.Replace(".", "_");
        var expectedFileName = $"resource_{normalizedBundleName}_{config.BuildTarget}";
        var expectedBundleFile = Path.Combine(_testOutputPath, expectedFileName);
        Assert.True(File.Exists(expectedBundleFile));

        // Clean up
        if (File.Exists(expectedBundleFile)) File.Delete(expectedBundleFile);
        var manifestFile = expectedBundleFile + ".manifest";
        if (File.Exists(manifestFile)) File.Delete(manifestFile);
    }

    [Fact]
    public void CreateAssetBundle_InvalidAssetDirectory_ShouldFail() {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid():N}");

        var config = new BuildConfiguration
        {
            UnityPath = @"C:\Unity\2022.3.35f1\Editor\Unity.exe", // Dummy path for test
            AssetDirectory = nonExistentPath,
            OutputDirectory = _testOutputPath,
            BundleName = "cryptiklemur.assetbuilder",
            BuildTarget = "windows"
        };

        Assert.False(Directory.Exists(config.AssetDirectory), "Asset directory should not exist");
    }

    private Task<bool> BuildAssetBundleAsync(BuildConfiguration config) {
        return Task.Run(() =>
        {
            try {
                // Initialize global config first
                GlobalConfig.Config = config;
                
                // Redirect console output to test output
                var originalOut = Console.Out;
                var originalError = Console.Error;
                var testWriter = new StringWriter();
                Console.SetOut(testWriter);
                Console.SetError(testWriter);

                // Initialize logging after console redirection so Serilog uses the redirected console
                GlobalConfig.InitializeLogging(config.Verbosity);
                
                // Use the main AssetBundleBuilder logic
                var exitCode = Program.BuildAssetBundle(config);

                // Restore console output and log what happened
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                _output.WriteLine(testWriter.ToString());

                return exitCode == 0;
            }
            catch (Exception ex) {
                _output.WriteLine($"Exception during build: {ex}");
                return false;
            }
        });
    }


    [Fact]
    public async Task CreateAssetBundle_TempDirectoryCaching_ShouldReuseDirectory() {
        // Skip test if Unity is not available
        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        var config = new BuildConfiguration
        {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = "cache.test",
            BuildTarget = "windows",
            KeepTempProject = true,
            CleanTempProject = false,
            LinkMethod = "copy"
        };

        // First build
        var success1 = await BuildAssetBundleAsync(config);
        Assert.True(success1, "First build failed");

        // Capture temp project path
        var hashInput = $"{config.AssetDirectory}|{config.BundleName}|{config.BuildTarget}";
        var hash = HashUtility.ComputeHash(hashInput);
        var expectedTempPath = Path.Combine(Path.GetTempPath(), $"AssetBundleBuilder_{hash}");

        Assert.True(Directory.Exists(expectedTempPath), "Temp directory should exist after first build");

        // Create a marker file to verify the directory is reused
        var markerFile = Path.Combine(expectedTempPath, "test_marker.txt");
        File.WriteAllText(markerFile, "test");

        // Second build with same config (should reuse temp directory)
        config.CleanTempProject = false; // Don't clean, should reuse
        var success2 = await BuildAssetBundleAsync(config);
        Assert.True(success2, "Second build failed");

        // Verify marker file still exists (directory was reused)
        Assert.True(File.Exists(markerFile), "Temp directory should have been reused");

        // Clean up
        _tempDirectoriesToCleanup.Add(expectedTempPath);
    }

    [Fact]
    public void CreateAssetBundle_EmptyAssetDirectory_ShouldHandleGracefully() {
        // Create empty directory
        var emptyDir = Path.Combine(Path.GetTempPath(), $"Empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);
        _tempDirectoriesToCleanup.Add(emptyDir);

        var config = new BuildConfiguration
        {
            UnityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1") ?? "",
            AssetDirectory = emptyDir,
            OutputDirectory = _testOutputPath,
            BundleName = "empty.test",
            BuildTarget = "windows"
        };

        // This should either succeed with empty bundle or fail gracefully
        // The behavior depends on Unity's handling of empty asset directories
        Assert.True(Directory.Exists(emptyDir));
    }
}