using System.CommandLine;
using System.Diagnostics;
using System.Text;
using CryptikLemur.AssetBundleBuilder.Config;
using CryptikLemur.AssetBundleBuilder.Utilities;
using Tomlet;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public abstract class AssetBundleTestBase : IDisposable {
    protected readonly ITestOutputHelper _output;
    protected readonly List<string> _tempDirectoriesToCleanup = [];
    protected readonly string _testAssetsPath;
    protected readonly string _testOutputPath;

    protected AssetBundleTestBase(ITestOutputHelper output, string testOutputDirectoryName = "TestOutput") {
        _output = output;
        _testAssetsPath =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "TestAssets"));
        _testOutputPath =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..",
                testOutputDirectoryName));

        // Clear and create TestOutput directory
        if (Directory.Exists(_testOutputPath)) {
            Directory.Delete(_testOutputPath, true);
        }

        Directory.CreateDirectory(_testOutputPath);
        _tempDirectoriesToCleanup.Add(_testOutputPath);
    }

    public virtual void Dispose() {
        foreach (string dir in _tempDirectoriesToCleanup) {
            if (Directory.Exists(dir)) {
                try {
                    string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    foreach (string file in files) {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(dir, true);
                    _output.WriteLine($"Cleaned up test directory: {dir}");
                } catch (Exception ex) {
                    _output.WriteLine($"Warning: Could not clean up {dir}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    ///     Create a simple test configuration for building a single bundle
    /// </summary>
    protected Configuration CreateTestConfiguration(
        string bundleName,
        string assetDirectory,
        string outputDirectory,
        bool targetless = true,
        List<string>? buildTargets = null,
        string? unityVersion = "2022.3.35f1",
        string? unityEditorPath = null,
        string? tempProjectPath = null) {
        // Create TOML string and parse it properly
        string sectionName = bundleName.Replace(".", "_");
        string tempProjectPathLine = tempProjectPath != null
            ? $"temp_project_path = \"{tempProjectPath.Replace("\\", "\\\\")}\""
            : "";
        string tomlContent = $@"
[global]
unity_version = ""{unityVersion ?? "2022.3.35f1"}""
unity_editor_path = ""{(unityEditorPath ?? FindTestUnityPath()).Replace("\\", "\\\\")}""
unity_hub_path = """"
{tempProjectPathLine}
clean_temp_project = true
link_method = ""copy""
allowed_targets = [""windows"", ""mac"", ""linux""]
targetless = {targetless.ToString().ToLower()}

[bundles.{sectionName}]
bundle_name = ""{bundleName}""
asset_directory = ""{assetDirectory.Replace("\\", "\\\\")}""
output_directory = ""{outputDirectory.Replace("\\", "\\\\")}""
targetless = {targetless.ToString().ToLower()}
";

        var tomlConfig = TomletMain.To<TomlConfiguration>(tomlContent);

        // Create a minimal mock ParseResult - we'll set properties directly after construction
        var mockParseResult = CommandLineParser.BuildRootCommand().Parse();
        var config = new Configuration(mockParseResult);

        // Override the properties with our test values
        config.BundleConfigNames = [sectionName]; // Use section name as the key, not bundle name
        config.BuildTargetList = buildTargets ?? [];
        config.ConfigFile = ""; // Not needed for tests
        config.TomlConfig = tomlConfig;

        return config;
    }

    /// <summary>
    ///     Build asset bundles using the new multi-bundle architecture
    /// </summary>
    protected async Task<bool> BuildAssetBundleAsync(Configuration config) {
        try {
            // Initialize global config first
            Program.Config = config;

            // Redirect console output to test output
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var testWriter = new StringWriter();
            Console.SetOut(testWriter);
            Console.SetError(testWriter);

            // Use the main AssetBundleBuilder logic
            int exitCode = await Program.BuildAssetBundles(config);

            // Restore console output and log what happened
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            _output.WriteLine(testWriter.ToString());

            return exitCode == 0;
        } catch (Exception ex) {
            _output.WriteLine($"Build failed with exception: {ex.Message}");
            _output.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    ///     Find a Unity executable for testing purposes
    /// </summary>
    private string FindTestUnityPath() {
        // Create a mock Unity executable for testing
        string mockPath = CreateMockUnityExecutable();

        // Set a flag to indicate we're using mock Unity
        Environment.SetEnvironmentVariable("USING_MOCK_UNITY", "true");

        return mockPath;
    }

    /// <summary>
    ///     Check if tests are using mock Unity execution
    /// </summary>
    protected bool IsUsingMockUnity() {
        return Environment.GetEnvironmentVariable("USING_MOCK_UNITY") == "true";
    }

    /// <summary>
    ///     Create a mock Unity executable that logs its arguments for verification
    /// </summary>
    private string CreateMockUnityExecutable() {
        string mockUnityDir = Path.Combine(_testOutputPath, "MockUnity");
        Directory.CreateDirectory(mockUnityDir);

        string logFile = Path.Combine(mockUnityDir, "unity_calls.log");
        Environment.SetEnvironmentVariable("MOCK_UNITY_LOG", logFile);

        // Create a simple script that uses shell execution  
        string mockUnityPath = Path.Combine(mockUnityDir, OperatingSystem.IsWindows() ? "Unity.bat" : "Unity.sh");

        if (OperatingSystem.IsWindows()) {
            string batchContent = $@"@echo off
echo Mock Unity called with args: %* >> ""{logFile}""
echo Working directory: %CD% >> ""{logFile}""
echo Mock Unity execution completed successfully >> ""{logFile}""
echo Mock Unity execution completed successfully
exit 0
";
            File.WriteAllText(mockUnityPath, batchContent);
        }
        else {
            // Create simple script content for Unix
            string scriptContent = "#!/bin/bash\n" +
                                   $"echo \"Mock Unity called with args: $*\" >> \"{logFile}\"\n" +
                                   $"echo \"Working directory: $(pwd)\" >> \"{logFile}\"\n" +
                                   $"echo \"Mock Unity execution completed successfully\" >> \"{logFile}\"\n" +
                                   "echo \"Mock Unity execution completed successfully\"\n" +
                                   "exit 0\n";

            // Write with explicit UTF-8 without BOM and Unix line endings
            File.WriteAllText(mockUnityPath, scriptContent, new UTF8Encoding(false));

            // Make executable
            try {
                var chmod = Process.Start("chmod", $"+x \"{mockUnityPath}\"");
                chmod?.WaitForExit();
            } catch {
                // Ignore chmod errors
            }
        }

        _output.WriteLine($"Created mock Unity at: {mockUnityPath}");
        _tempDirectoriesToCleanup.Add(mockUnityDir);

        return mockUnityPath;
    }


    /// <summary>
    ///     Get the arguments that were passed to the mock Unity executable
    /// </summary>
    protected string GetMockUnityLog() {
        string logFile = Environment.GetEnvironmentVariable("MOCK_UNITY_LOG") ?? "";
        return File.Exists(logFile) ? File.ReadAllText(logFile) : "";
    }

    /// <summary>
    ///     Verify that Unity was called with expected arguments
    /// </summary>
    protected void VerifyUnityWasCalled() {
        string log = GetMockUnityLog();
        Assert.False(string.IsNullOrEmpty(log), "Unity should have been called");
        _output.WriteLine($"Mock Unity log:\n{log}");
    }

    /// <summary>
    ///     Verify that Unity was called with specific arguments
    /// </summary>
    protected void VerifyUnityCalledWithArguments(params string[] expectedArgs) {
        string log = GetMockUnityLog();
        Assert.False(string.IsNullOrEmpty(log), "Unity should have been called");

        foreach (string expectedArg in expectedArgs) {
            Assert.Contains(expectedArg, log);
        }

        _output.WriteLine($"Verified Unity called with expected arguments: {string.Join(", ", expectedArgs)}");
    }

    /// <summary>
    ///     Verify asset bundle files were created, but skip verification if using mock Unity
    /// </summary>
    protected void VerifyAssetBundleFilesOrSkip(string outputPath, string expectedFileName, string context = "") {
        if (IsUsingMockUnity()) {
            _output.WriteLine($"Skipping file verification for mock Unity - {context}");
            _output.WriteLine($"  Expected file: {expectedFileName}");
            return;
        }

        string expectedFile = Path.Combine(outputPath, expectedFileName);
        string manifestFile = expectedFile + ".manifest";

        Assert.True(File.Exists(expectedFile), $"Expected asset bundle file not found: {expectedFileName}");
        Assert.True(File.Exists(manifestFile), $"Expected manifest file not found: {expectedFileName}.manifest");

        _output.WriteLine($"{context}: {expectedFileName}");
    }

    /// <summary>
    ///     Create test assets directory with some basic files
    /// </summary>
    protected string CreateTestAssetsDirectory(string subdirectory = "TestBundle") {
        string testAssetsDir = Path.Combine(_testAssetsPath, subdirectory);

        if (Directory.Exists(testAssetsDir)) {
            Directory.Delete(testAssetsDir, true);
        }

        Directory.CreateDirectory(testAssetsDir);

        // Create some test files
        File.WriteAllText(Path.Combine(testAssetsDir, "test.txt"), "Test file content");
        File.WriteAllText(Path.Combine(testAssetsDir, "test.xml"), "<test>XML content</test>");

        string subdirPath = Path.Combine(testAssetsDir, "Subfolder");
        Directory.CreateDirectory(subdirPath);
        File.WriteAllText(Path.Combine(subdirPath, "nested.txt"), "Nested file content");

        _tempDirectoriesToCleanup.Add(testAssetsDir);
        return testAssetsDir;
    }

    /// <summary>
    ///     Verify that expected asset bundle files exist
    /// </summary>
    protected void VerifyAssetBundleFiles(string outputDirectory, string expectedBundleName, bool targetless = true,
        List<string>? targets = null) {
        if (targetless) {
            // For targetless bundles, expect resource_bundlename format
            string expectedFile = Path.Combine(outputDirectory, $"resource_{expectedBundleName.Replace(".", "_")}");
            string manifestFile = expectedFile + ".manifest";

            Assert.True(File.Exists(expectedFile), $"Expected targetless bundle file not found: {expectedFile}");
            Assert.True(File.Exists(manifestFile), $"Expected manifest file not found: {manifestFile}");

            _output.WriteLine($"Found targetless bundle: {expectedFile}");
        }
        else {
            // For targeted bundles, expect resource_bundlename_target format
            targets ??= ["windows", "mac", "linux"];
            foreach (string target in targets) {
                string platformSuffix = target switch {
                    "windows" => "win",
                    "mac" => "mac",
                    "linux" => "linux",
                    _ => target
                };

                string expectedFile = Path.Combine(outputDirectory,
                    $"resource_{expectedBundleName.Replace(".", "_")}_{platformSuffix}");
                string manifestFile = expectedFile + ".manifest";

                Assert.True(File.Exists(expectedFile), $"Expected targeted bundle file not found: {expectedFile}");
                Assert.True(File.Exists(manifestFile), $"Expected manifest file not found: {manifestFile}");

                _output.WriteLine($"Found targeted bundle for {target}: {expectedFile}");
            }
        }
    }
}