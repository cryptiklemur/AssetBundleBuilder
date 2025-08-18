using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class ExcludePatternTests {
    // Use reflection to test private methods
    private static bool IsExcluded(string relativePath, List<string> excludePatterns) {
        var type = typeof(Program);
        var method = type.GetMethod("IsExcluded", BindingFlags.NonPublic | BindingFlags.Static);
        return (bool)method!.Invoke(null, new object[] { relativePath, excludePatterns })!;
    }

    private static string GlobToRegex(string glob) {
        var type = typeof(Program);
        var method = type.GetMethod("GlobToRegex", BindingFlags.NonPublic | BindingFlags.Static);
        return (string)method!.Invoke(null, new object[] { glob })!;
    }

    [Theory]
    [InlineData("*.tmp", "file.tmp", true)]
    [InlineData("*.tmp", "file.txt", false)]
    [InlineData("*.tmp", "path/to/file.tmp", true)]
    [InlineData("backup/*", "backup/file.txt", true)]
    [InlineData("backup/*", "other/file.txt", false)]
    [InlineData("**/*.log", "deep/nested/path/error.log", true)]
    [InlineData("**/*.log", "error.log", true)]
    [InlineData("temp*", "temporary.txt", true)]
    [InlineData("temp*", "other.txt", false)]
    [InlineData("*.bak", "file.bak", true)]
    [InlineData("*.bak", "file.backup", false)]
    public void IsExcluded_WithVariousPatterns_ShouldMatchCorrectly(string pattern, string path, bool expected) {
        var patterns = new List<string> { pattern };
        var result = IsExcluded(path, patterns);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsExcluded_WithMultiplePatterns_ShouldMatchAny() {
        var patterns = new List<string> { "*.tmp", "*.bak", "backup/*" };
        
        Assert.True(IsExcluded("file.tmp", patterns));
        Assert.True(IsExcluded("file.bak", patterns));
        Assert.True(IsExcluded("backup/anything.txt", patterns));
        Assert.False(IsExcluded("file.txt", patterns));
    }

    [Fact]
    public void IsExcluded_WithNullPatterns_ShouldReturnFalse() {
        Assert.False(IsExcluded("any/path.txt", null!));
    }

    [Fact]
    public void IsExcluded_WithEmptyPatterns_ShouldReturnFalse() {
        Assert.False(IsExcluded("any/path.txt", new List<string>()));
    }

    [Theory]
    [InlineData("*.txt", @"^(^|.*/)[^/]*\.txt$")]
    [InlineData("backup/*", @"^(^|.*/)backup/[^/]*$")]
    [InlineData("**/*.log", @"^(.*/)?[^/]*\.log$")]
    [InlineData("/absolute/*.txt", @"^/absolute/[^/]*\.txt$")]
    [InlineData("dir/", @"^(^|.*/)dir/.*$")]
    public void GlobToRegex_ShouldConvertCorrectly(string glob, string expectedRegex) {
        var result = GlobToRegex(glob);
        Assert.Equal(expectedRegex, result);
    }

    [Theory]
    [InlineData("file.tmp", "file.tmp")]
    [InlineData(@"path\to\file.tmp", "path/to/file.tmp")]
    [InlineData(@"C:\Users\test\file.tmp", "C:/Users/test/file.tmp")]
    public void IsExcluded_WithWindowsPaths_ShouldNormalize(string windowsPath, string normalizedPath) {
        var patterns = new List<string> { "*.tmp" };
        
        // Both should match because paths are normalized
        var windowsResult = IsExcluded(windowsPath, patterns);
        var normalizedResult = IsExcluded(normalizedPath, patterns);
        
        Assert.True(windowsResult);
        Assert.True(normalizedResult);
        Assert.Equal(windowsResult, normalizedResult);
    }

    [Fact]
    public void IsExcluded_CaseInsensitive_ShouldMatch() {
        var patterns = new List<string> { "*.TMP" };
        
        Assert.True(IsExcluded("file.tmp", patterns));
        Assert.True(IsExcluded("FILE.TMP", patterns));
        Assert.True(IsExcluded("File.Tmp", patterns));
    }
}