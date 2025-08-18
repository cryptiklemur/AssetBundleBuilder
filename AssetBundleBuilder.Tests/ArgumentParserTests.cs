using System.Runtime.InteropServices;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class ArgumentParserTests {
    private static string GetTestPath(string relativePath) {
        // Create cross-platform test paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine("C:", relativePath);

        return Path.Combine("/", relativePath);
    }

    [Fact]
    public void Parse_MinimalArgs_ShouldParseCorrectly() {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { "2022.3.35f1", assetPath, "testmod", outputPath };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal("2022.3.35f1", config.UnityVersion);
        Assert.Equal(Path.GetFullPath(assetPath), config.AssetDirectory);
        Assert.Equal(Path.GetFullPath(outputPath), config.OutputDirectory);
        Assert.Equal("testmod", config.BundleName);
        Assert.Equal("", config.BuildTarget); // Default is empty - auto-detected current OS without platform suffix
        Assert.Equal("copy", config.LinkMethod);
    }

    [Fact]
    public void Parse_WithUnityPath_ShouldParseCorrectly() {
        using var tempUnity = new TempUnityFile("Unity");
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { tempUnity.Path, assetPath, "testmod", outputPath };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal(tempUnity.Path, config.UnityPath);
        Assert.Equal(Path.GetFullPath(assetPath), config.AssetDirectory);
        Assert.Equal(Path.GetFullPath(outputPath), config.OutputDirectory);
        Assert.Equal("testmod", config.BundleName);
    }

    [Fact]
    public void Parse_WithBuildTarget_ShouldParseCorrectly() {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { "2022.3.35f1", assetPath, "testmod", outputPath, "--target", "windows" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal("windows", config.BuildTarget);
    }

    [Fact]
    public void Parse_WithLinkMethods_ShouldParseCorrectly() {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args1 = new[] { "2022.3.35f1", assetPath, "test", outputPath, "--symlink" };
        var config1 = ArgumentParser.Parse(args1);
        Assert.Equal("symlink", config1?.LinkMethod);

        var args2 = new[] { "2022.3.35f1", assetPath, "test", outputPath, "--hardlink" };
        var config2 = ArgumentParser.Parse(args2);
        Assert.Equal("hardlink", config2?.LinkMethod);

        var args3 = new[] { "2022.3.35f1", assetPath, "test", outputPath, "--junction" };
        var config3 = ArgumentParser.Parse(args3);
        Assert.Equal("junction", config3?.LinkMethod);

        var args4 = new[] { "2022.3.35f1", assetPath, "test", outputPath, "--copy" };
        var config4 = ArgumentParser.Parse(args4);
        Assert.Equal("copy", config4?.LinkMethod);
    }

    [Fact]
    public void Parse_WithTempOptions_ShouldParseCorrectly() {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args2 = new[] { "2022.3.35f1", assetPath, "test", outputPath, "--clean-temp" };
        var config2 = ArgumentParser.Parse(args2);
        Assert.True(config2?.CleanTempProject);

        var customTempPath = GetTestPath("CustomTemp");
        var args3 = new[] { "2022.3.35f1", assetPath, "test", outputPath, "--temp-project", customTempPath };
        var config3 = ArgumentParser.Parse(args3);
        Assert.Equal(Path.GetFullPath(customTempPath), config3?.TempProjectPath);
    }

    [Fact]
    public void Parse_WithExplicitUnityVersion_ShouldParseCorrectly() {
        using var tempUnity = new TempUnityFile("TestUnity");
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { tempUnity.Path, assetPath, "test", outputPath, "--unity-version", "2022.3.35f1" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal(tempUnity.Path, config.UnityPath);
        Assert.Equal("2022.3.35f1", config.UnityVersion);
    }

    [Fact]
    public void Parse_TooFewArgs_ShouldReturnNull() {
        var args = new[] { "2022.3.35f1" };
        var config = ArgumentParser.Parse(args);
        Assert.Null(config);
    }

    [Theory]
    [InlineData("test.framework")]
    [InlineData("my.bundle")]
    [InlineData("Test.Framework")]
    [InlineData("My.Bundle")]
    public void Parse_BundleNameWithForbiddenExtension_ShouldThrowException(string bundleName) {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { "2022.3.35f1", assetPath, bundleName, outputPath };
        
        var exception = Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(args));
        Assert.Contains("cannot end with .framework or .bundle", exception.Message);
        Assert.Contains(bundleName.ToLower(), exception.Message);
    }

    [Fact]
    public void Parse_WithRelativePaths_ShouldConvertToAbsolute() {
        var currentDir = Directory.GetCurrentDirectory();
        var args = new[] { "2022.3.35f1", "Assets", "test", "Output" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal(Path.GetFullPath(Path.Combine(currentDir, "Assets")), config.AssetDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(currentDir, "Output")), config.OutputDirectory);
    }

    [Fact]
    public void Parse_WithSingleExclude_ShouldAddPattern() {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { "2022.3.35f1", assetPath, "testmod", outputPath, "--exclude", "*.tmp" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Single(config.ExcludePatterns);
        Assert.Contains("*.tmp", config.ExcludePatterns);
    }

    [Fact]
    public void Parse_WithMultipleExcludes_ShouldAddAllPatterns() {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { "2022.3.35f1", assetPath, "testmod", outputPath, 
            "--exclude", "*.tmp", "--exclude", "backup/*", "--exclude", "*.bak" };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Equal(3, config.ExcludePatterns.Count);
        Assert.Contains("*.tmp", config.ExcludePatterns);
        Assert.Contains("backup/*", config.ExcludePatterns);
        Assert.Contains("*.bak", config.ExcludePatterns);
    }

    [Fact]
    public void Parse_WithoutExclude_ShouldHaveEmptyList() {
        var assetPath = GetTestPath("Assets");
        var outputPath = GetTestPath("Output");
        var args = new[] { "2022.3.35f1", assetPath, "testmod", outputPath };
        var config = ArgumentParser.Parse(args);

        Assert.NotNull(config);
        Assert.Empty(config.ExcludePatterns);
    }

    private class TempUnityFile : IDisposable {
        public TempUnityFile(string baseFileName) {
            // Use platform-appropriate Unity executable name
            var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $"{baseFileName}.exe"
                : baseFileName;

            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            File.WriteAllText(Path, "dummy");
        }

        public string Path { get; }

        public void Dispose() {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}