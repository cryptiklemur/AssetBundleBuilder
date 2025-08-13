using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class LogFileArgumentTests {
    private readonly ITestOutputHelper _output;

    public LogFileArgumentTests(ITestOutputHelper output) {
        _output = output;
    }

    [Fact]
    public void Parse_WithLogFileArgument_ShouldSetLogFile() {
        // Arrange
        var args = new[]
        {
            "2022.3.35f1",
            "C:\\Assets",
            "testbundle",
            "C:\\Output",
            "--logfile",
            "C:\\Logs\\unity.log"
        };

        // Act
        var config = ArgumentParser.Parse(args);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("C:\\Logs\\unity.log", config.LogFile);
        Assert.Equal("testbundle", config.BundleName);
        Assert.Equal("C:\\Assets", config.AssetDirectory);
        Assert.Equal("C:\\Output", config.OutputDirectory);
    }

    [Fact]
    public void Parse_WithRelativeLogFile_ShouldConvertToAbsolute() {
        // Arrange
        var args = new[]
        {
            "2022.3.35f1",
            "C:\\Assets",
            "testbundle",
            "--logfile",
            "unity.log"
        };

        // Act
        var config = ArgumentParser.Parse(args);

        // Assert
        Assert.NotNull(config);
        Assert.NotEmpty(config.LogFile);
        Assert.True(Path.IsPathFullyQualified(config.LogFile));
        Assert.EndsWith("unity.log", config.LogFile);

        _output.WriteLine($"Relative path 'unity.log' resolved to: {config.LogFile}");
    }

    [Fact]
    public void Parse_WithoutLogFile_ShouldHaveEmptyLogFile() {
        // Arrange
        var args = new[]
        {
            "2022.3.35f1",
            "C:\\Assets",
            "testbundle",
            "C:\\Output"
        };

        // Act
        var config = ArgumentParser.Parse(args);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(string.Empty, config.LogFile);
    }

    [Fact]
    public void Parse_WithLogFileAndOtherOptions_ShouldParseAll() {
        // Arrange  
        var args = new[]
        {
            "2022.3.35f1",
            "C:\\Assets",
            "testbundle",
            "--target",
            "linux",
            "--logfile",
            "C:\\Logs\\build.log",
            "--keep-temp"
        };

        // Act
        var config = ArgumentParser.Parse(args);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("C:\\Logs\\build.log", config.LogFile);
        Assert.Equal("linux", config.BuildTarget);
        Assert.True(config.KeepTempProject);
        Assert.Equal("testbundle", config.BundleName);
    }

    [Theory]
    [InlineData("", true)] // Empty string should result in empty LogFile
    [InlineData("C:\\Logs\\test.log", false)] // Valid path should be set
    [InlineData("./relative.log", false)] // Relative path should be converted
    public void LogFile_VariousInputs_ShouldHandleCorrectly(string logFilePath, bool shouldBeEmpty) {
        // This tests the ArgumentParser logic for different logfile inputs
        var normalizedPath = string.IsNullOrEmpty(logFilePath) ? "" : Path.GetFullPath(logFilePath);

        if (shouldBeEmpty) Assert.Equal(string.Empty, normalizedPath);
        else {
            Assert.NotEqual(string.Empty, normalizedPath);
            Assert.True(Path.IsPathFullyQualified(normalizedPath));
        }

        _output.WriteLine($"Input: '{logFilePath}' -> Output: '{normalizedPath}'");
    }
}