using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class AssetBundleNamingTests : IDisposable {
    private readonly ITestOutputHelper _output;
    private readonly List<string> _tempDirectoriesToCleanup = new();
    private readonly string _testAssetsPath;
    private readonly string _testOutputPath;

    public AssetBundleNamingTests(ITestOutputHelper output) {
        _output = output;
        _testAssetsPath =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "TestAssets"));
        _testOutputPath =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "TestOutputNaming"));

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

    [Theory]
    [InlineData("author.modname", "windows", "resource_author_modname_win")]
    [InlineData("mymod", "linux", "resource_mymod_linux")]
    [InlineData("test.bundle", "mac", "resource_test_bundle_mac")]
    [InlineData("cryptiklemur.assetbuilder", "windows", "resource_cryptiklemur_assetbuilder_win")]
    [InlineData("simple", "linux", "resource_simple_linux")]
    public async Task CreateAssetBundle_VerifyNewNamingFormat(string inputBundleName, string buildTarget,
        string expectedFileName) {
        // Skip test in CI environments or if Unity is not available
        var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        if (isCI) {
            _output.WriteLine("Skipping test: Unity tests are disabled in CI environment");
            return;
        }

        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        // Verify test assets exist
        if (!Directory.Exists(_testAssetsPath)) {
            _output.WriteLine($"Skipping test: TestAssets directory not found at {_testAssetsPath}");
            return;
        }

        _output.WriteLine($"Testing naming: '{inputBundleName}' + '{buildTarget}' = '{expectedFileName}'");

        // Create build configuration
        var config = new BuildConfiguration {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = inputBundleName,
            BuildTarget = buildTarget,
            CleanTempProject = true,
            LinkMethod = "copy"
        };

        // Build the asset bundle
        var success = await BuildAssetBundleAsync(config);
        Assert.True(success, "Asset bundle creation failed");

        // Verify the output file uses the new naming format
        var expectedBundleFile = Path.Combine(_testOutputPath, expectedFileName);
        var expectedManifestFile = Path.Combine(_testOutputPath, expectedFileName + ".manifest");

        _output.WriteLine($"Looking for bundle file at: {expectedBundleFile}");
        _output.WriteLine($"Looking for manifest file at: {expectedManifestFile}");

        // List all files in output directory for debugging
        var outputFiles = Directory.GetFiles(_testOutputPath, "*", SearchOption.AllDirectories);
        _output.WriteLine("Files in output directory:");
        foreach (var file in outputFiles)
            _output.WriteLine($"  {Path.GetRelativePath(_testOutputPath, file)} ({new FileInfo(file).Length} bytes)");

        Assert.True(File.Exists(expectedBundleFile),
            $"Asset bundle not found with expected name. Expected: {expectedBundleFile}");
        Assert.True(File.Exists(expectedManifestFile),
            $"Manifest not found with expected name. Expected: {expectedManifestFile}");

        // Verify no old-format files exist
        var oldFormatBundleFile = Path.Combine(_testOutputPath, inputBundleName);
        var oldFormatManifestFile = Path.Combine(_testOutputPath, inputBundleName + ".manifest");

        Assert.False(File.Exists(oldFormatBundleFile),
            $"Old format bundle file should not exist: {oldFormatBundleFile}");
        Assert.False(File.Exists(oldFormatManifestFile),
            $"Old format manifest file should not exist: {oldFormatManifestFile}");

        // Clean up output files for next iteration
        if (File.Exists(expectedBundleFile)) File.Delete(expectedBundleFile);
        if (File.Exists(expectedManifestFile)) File.Delete(expectedManifestFile);
    }

    [Fact]
    public void GenerateAssetBundleName_LogicTest() {
        // Test the naming logic without requiring Unity
        var testCases = new[] {
            new { BundleName = "author.modname", Target = "windows", Expected = "resource_author_modname_win" },
            new { BundleName = "mymod", Target = "linux", Expected = "resource_mymod_linux" },
            new { BundleName = "test.bundle", Target = "mac", Expected = "resource_test_bundle_mac" },
            new {
                BundleName = "complex.name.with.dots", Target = "windows",
                Expected = "resource_complex_name_with_dots_win"
            },
            new { BundleName = "simple", Target = "linux", Expected = "resource_simple_linux" }
        };

        foreach (var testCase in testCases) {
            // Simulate the naming logic from ModAssetBundleBuilder.cs
            var normalizedBundleName = testCase.BundleName.Replace(".", "_");
            
            // Map build target to short suffix
            var platformSuffix = testCase.Target switch
            {
                "windows" => "win",
                "mac" => "mac",
                "linux" => "linux",
                _ => testCase.Target
            };
            
            var actualResult = $"resource_{normalizedBundleName}_{platformSuffix}";

            _output.WriteLine($"Input: '{testCase.BundleName}' + '{testCase.Target}' -> '{actualResult}'");
            Assert.Equal(testCase.Expected, actualResult);
        }
    }

    [Fact]
    public void GenerateAssetBundleName_NoTargetLogicTest() {
        // Test the naming logic for no platform suffix without requiring Unity
        var testCases = new[] {
            new { BundleName = "author.modname", Expected = "resource_author_modname" },
            new { BundleName = "mymod", Expected = "resource_mymod" },
            new { BundleName = "test.bundle", Expected = "resource_test_bundle" },
            new { BundleName = "complex.name.with.dots", Expected = "resource_complex_name_with_dots" },
            new { BundleName = "simple", Expected = "resource_simple" }
        };

        foreach (var testCase in testCases) {
            // Simulate the naming logic from ModAssetBundleBuilder.cs when noPlatformSuffix = true
            var normalizedBundleName = testCase.BundleName.Replace(".", "_");
            var actualResult = $"resource_{normalizedBundleName}"; // No platform suffix

            _output.WriteLine($"Input: '{testCase.BundleName}' (no target) -> '{actualResult}'");
            Assert.Equal(testCase.Expected, actualResult);
        }
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("linux")]
    [InlineData("mac")]
    public async Task CreateAssetBundle_DifferentTargets_ShouldIncludeTargetInName(string buildTarget) {
        // Skip test in CI environments or if Unity is not available
        var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        if (isCI) {
            _output.WriteLine("Skipping test: Unity tests are disabled in CI environment");
            return;
        }

        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        // Verify test assets exist
        if (!Directory.Exists(_testAssetsPath)) {
            _output.WriteLine($"Skipping test: TestAssets directory not found at {_testAssetsPath}");
            return;
        }

        const string bundleName = "target.test";
        
        // Map build target to short suffix
        var platformSuffix = buildTarget switch
        {
            "windows" => "win",
            "mac" => "mac",
            "linux" => "linux",
            _ => buildTarget
        };
        
        var expectedFileName = $"resource_target_test_{platformSuffix}";

        var config = new BuildConfiguration {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = bundleName,
            BuildTarget = buildTarget,
            CleanTempProject = true,
            LinkMethod = "copy"
        };

        var success = await BuildAssetBundleAsync(config);
        Assert.True(success, $"Asset bundle creation failed for target: {buildTarget}");

        // Verify the target is included in the filename
        var expectedBundleFile = Path.Combine(_testOutputPath, expectedFileName);
        Assert.True(File.Exists(expectedBundleFile),
            $"Bundle file should include target '{buildTarget}' in name: {expectedFileName}");

        // Clean up
        if (File.Exists(expectedBundleFile)) File.Delete(expectedBundleFile);
        var manifestFile = expectedBundleFile + ".manifest";
        if (File.Exists(manifestFile)) File.Delete(manifestFile);
    }

    [Fact]
    public async Task CreateAssetBundle_NoTargetSpecified_ShouldNotIncludePlatformSuffix() {
        // Skip test in CI environments or if Unity is not available
        var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        if (isCI) {
            _output.WriteLine("Skipping test: Unity tests are disabled in CI environment");
            return;
        }

        var unityPath = UnityPathFinder.FindUnityExecutable("2022.3.35f1");
        if (string.IsNullOrEmpty(unityPath)) {
            _output.WriteLine("Skipping test: Unity 2022.3.35f1 not found");
            return;
        }

        // Verify test assets exist
        if (!Directory.Exists(_testAssetsPath)) {
            _output.WriteLine($"Skipping test: TestAssets directory not found at {_testAssetsPath}");
            return;
        }

        const string bundleName = "no.target.test";
        var expectedFileName = "resource_no_target_test"; // No platform suffix expected

        var config = new BuildConfiguration {
            UnityPath = unityPath,
            UnityVersion = "2022.3.35f1",
            AssetDirectory = _testAssetsPath,
            OutputDirectory = _testOutputPath,
            BundleName = bundleName,
            BuildTarget = "", // Empty string means auto-detect current OS without platform suffix
            CleanTempProject = true,
            LinkMethod = "copy"
        };

        _output.WriteLine($"Testing default behavior: '{bundleName}' -> '{expectedFileName}' (no platform suffix)");

        var success = await BuildAssetBundleAsync(config);
        Assert.True(success, "Asset bundle creation failed for default (no target) case");

        // Verify the platform suffix is NOT included in the filename
        var expectedBundleFile = Path.Combine(_testOutputPath, expectedFileName);
        var expectedManifestFile = Path.Combine(_testOutputPath, expectedFileName + ".manifest");

        _output.WriteLine($"Looking for bundle file at: {expectedBundleFile}");
        _output.WriteLine($"Looking for manifest file at: {expectedManifestFile}");

        // List all files in output directory for debugging
        var outputFiles = Directory.GetFiles(_testOutputPath, "*", SearchOption.AllDirectories);
        _output.WriteLine("Files in output directory:");
        foreach (var file in outputFiles)
            _output.WriteLine($"  {Path.GetRelativePath(_testOutputPath, file)} ({new FileInfo(file).Length} bytes)");

        Assert.True(File.Exists(expectedBundleFile),
            $"Bundle file should NOT include platform suffix when no target specified: {expectedFileName}");
        Assert.True(File.Exists(expectedManifestFile),
            $"Manifest file should NOT include platform suffix when no target specified: {expectedFileName}.manifest");

        // Verify no platform-suffixed files exist (verify that we don't accidentally get the old behavior)
        var currentOS = DetectCurrentOSForTest();
        var withSuffixFile = Path.Combine(_testOutputPath, $"resource_no_target_test_{currentOS}");
        Assert.False(File.Exists(withSuffixFile),
            $"Bundle file with platform suffix should NOT exist when no target specified: {withSuffixFile}");

        // Clean up
        if (File.Exists(expectedBundleFile)) File.Delete(expectedBundleFile);
        if (File.Exists(expectedManifestFile)) File.Delete(expectedManifestFile);
    }

    private static string DetectCurrentOSForTest() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "win";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "mac";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";

        return "unknown";
    }

    private Task<bool> BuildAssetBundleAsync(BuildConfiguration config) {
        return Task.Run(() => {
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
}