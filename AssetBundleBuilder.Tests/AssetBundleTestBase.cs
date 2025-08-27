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

    protected Task<bool> BuildAssetBundleAsync(Configuration config) {
        return Task.Run(() => {
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