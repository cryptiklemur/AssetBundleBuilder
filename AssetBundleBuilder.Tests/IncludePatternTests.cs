using System.Reflection;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class IncludePatternTests {
    // Use reflection to test private methods
    private static bool IsIncluded(string relativePath, List<string> includePatterns) {
        var type = typeof(Program);
        var method = type.GetMethod("IsIncluded", BindingFlags.NonPublic | BindingFlags.Static);
        return (bool)method!.Invoke(null, new object[] { relativePath, includePatterns })!;
    }

    [Theory]
    [InlineData("*.png", "image.png", true)]
    [InlineData("*.png", "image.jpg", false)]
    [InlineData("*.png", "path/to/image.png", true)]
    [InlineData("textures/*", "textures/sprite.png", true)]
    [InlineData("textures/*", "other/sprite.png", false)]
    [InlineData("**/*.png", "deep/nested/path/image.png", true)]
    [InlineData("**/*.png", "image.png", true)]
    [InlineData("Assets/Sounds", "Assets/Sounds", true)]
    [InlineData("Assets/Sounds/*", "Assets/Sounds/music.wav", true)]
    [InlineData("Assets/Sounds/*", "Assets/Textures/image.png", false)]
    public void IsIncluded_WithVariousPatterns_ShouldMatchCorrectly(string pattern, string path, bool expected) {
        var patterns = new List<string> { pattern };
        var result = IsIncluded(path, patterns);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsIncluded_WithMultiplePatterns_ShouldMatchAny() {
        var patterns = new List<string> { "*.png", "*.jpg", "sounds/*" };
        
        Assert.True(IsIncluded("image.png", patterns));
        Assert.True(IsIncluded("photo.jpg", patterns));
        Assert.True(IsIncluded("sounds/music.wav", patterns));
        Assert.False(IsIncluded("document.txt", patterns));
    }

    [Fact]
    public void IsIncluded_WithNullPatterns_ShouldReturnTrue() {
        Assert.True(IsIncluded("any/path.txt", null!));
    }

    [Fact]
    public void IsIncluded_WithEmptyPatterns_ShouldReturnTrue() {
        Assert.True(IsIncluded("any/path.txt", new List<string>()));
    }

    [Theory]
    [InlineData("file.png", "file.png")]
    [InlineData(@"path\to\file.png", "path/to/file.png")]
    [InlineData(@"C:\Users\test\file.png", "C:/Users/test/file.png")]
    public void IsIncluded_WithWindowsPaths_ShouldNormalize(string windowsPath, string normalizedPath) {
        var patterns = new List<string> { "*.png" };
        
        // Both should match because paths are normalized
        var windowsResult = IsIncluded(windowsPath, patterns);
        var normalizedResult = IsIncluded(normalizedPath, patterns);
        
        Assert.True(windowsResult);
        Assert.True(normalizedResult);
        Assert.Equal(windowsResult, normalizedResult);
    }

    [Fact]
    public void IsIncluded_CaseInsensitive_ShouldMatch() {
        var patterns = new List<string> { "*.PNG" };
        
        Assert.True(IsIncluded("file.png", patterns));
        Assert.True(IsIncluded("FILE.PNG", patterns));
        Assert.True(IsIncluded("File.Png", patterns));
    }

    [Fact]
    public void IsIncluded_DirectoryPatterns_ShouldWork() {
        var patterns = new List<string> { "Assets/Sounds", "Assets/Textures" };
        
        Assert.True(IsIncluded("Assets/Sounds", patterns));
        Assert.True(IsIncluded("Assets/Textures", patterns));
        Assert.False(IsIncluded("Assets/Scripts", patterns));
        Assert.False(IsIncluded("Other/Sounds", patterns));
    }

    [Fact]
    public void IsIncluded_DirectoryWithWildcard_ShouldMatchContents() {
        var patterns = new List<string> { "Assets/Sounds/*" };
        
        Assert.True(IsIncluded("Assets/Sounds/music.wav", patterns));
        Assert.True(IsIncluded("Assets/Sounds/effect.mp3", patterns));
        Assert.False(IsIncluded("Assets/Sounds", patterns)); // Directory itself doesn't match
        Assert.False(IsIncluded("Assets/Textures/image.png", patterns));
    }

    [Fact]
    public void IsIncluded_DeepDirectoryWildcard_ShouldMatchNested() {
        var patterns = new List<string> { "Assets/Sounds/**" };
        
        Assert.True(IsIncluded("Assets/Sounds/music/track1.wav", patterns));
        Assert.True(IsIncluded("Assets/Sounds/effects/ambient/wind.mp3", patterns));
        Assert.True(IsIncluded("Assets/Sounds/file.wav", patterns));
        Assert.False(IsIncluded("Assets/Textures/image.png", patterns));
    }
}