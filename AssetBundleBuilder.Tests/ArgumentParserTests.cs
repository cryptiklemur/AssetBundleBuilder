using System.IO;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class ArgumentParserTests
{
    private class TempUnityFile : IDisposable
    {
        public string Path { get; }
        
        public TempUnityFile(string fileName)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            File.WriteAllText(Path, "dummy");
        }
        
        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
    [Fact]
    public void Parse_MinimalArgs_ShouldParseCorrectly()
    {
        var args = new[] { "2022.3.35f1", @"C:\Assets", "test.bundle", @"C:\Output" };
        var config = ArgumentParser.Parse(args);
        
        Assert.NotNull(config);
        Assert.Equal("2022.3.35f1", config.UnityVersion);
        Assert.Equal(@"C:\Assets", config.AssetDirectory);
        Assert.Equal(@"C:\Output", config.OutputDirectory);
        Assert.Equal("test.bundle", config.BundleName);
        Assert.Equal("windows", config.BuildTarget);
        Assert.Equal("copy", config.LinkMethod);
    }

    [Fact]
    public void Parse_WithUnityPath_ShouldParseCorrectly()
    {
        using var tempUnity = new TempUnityFile("Unity.exe");
        var args = new[] { tempUnity.Path, @"C:\Assets", "test.bundle", @"C:\Output" };
        var config = ArgumentParser.Parse(args);
        
        Assert.NotNull(config);
        Assert.Equal(tempUnity.Path, config.UnityPath);
        Assert.Equal(@"C:\Assets", config.AssetDirectory);
        Assert.Equal(@"C:\Output", config.OutputDirectory);
        Assert.Equal("test.bundle", config.BundleName);
    }

    [Fact]
    public void Parse_WithBuildTarget_ShouldParseCorrectly()
    {
        var args = new[] { "2022.3.35f1", @"C:\Assets", "test.bundle", @"C:\Output", "--target", "windows" };
        var config = ArgumentParser.Parse(args);
        
        Assert.NotNull(config);
        Assert.Equal("windows", config.BuildTarget);
    }

    [Fact]
    public void Parse_WithLinkMethods_ShouldParseCorrectly()
    {
        var args1 = new[] { "2022.3.35f1", @"C:\Assets", "test", @"C:\Output", "--symlink" };
        var config1 = ArgumentParser.Parse(args1);
        Assert.Equal("symlink", config1?.LinkMethod);

        var args2 = new[] { "2022.3.35f1", @"C:\Assets", "test", @"C:\Output", "--hardlink" };
        var config2 = ArgumentParser.Parse(args2);
        Assert.Equal("hardlink", config2?.LinkMethod);

        var args3 = new[] { "2022.3.35f1", @"C:\Assets", "test", @"C:\Output", "--junction" };
        var config3 = ArgumentParser.Parse(args3);
        Assert.Equal("junction", config3?.LinkMethod);

        var args4 = new[] { "2022.3.35f1", @"C:\Assets", "test", @"C:\Output", "--copy" };
        var config4 = ArgumentParser.Parse(args4);
        Assert.Equal("copy", config4?.LinkMethod);
    }

    [Fact]
    public void Parse_WithTempOptions_ShouldParseCorrectly()
    {
        var args1 = new[] { "2022.3.35f1", @"C:\Assets", "test", @"C:\Output", "--keep-temp" };
        var config1 = ArgumentParser.Parse(args1);
        Assert.True(config1?.KeepTempProject);

        var args2 = new[] { "2022.3.35f1", @"C:\Assets", "test", @"C:\Output", "--clean-temp" };
        var config2 = ArgumentParser.Parse(args2);
        Assert.True(config2?.CleanTempProject);

        var args3 = new[] { "2022.3.35f1", @"C:\Assets", "test", @"C:\Output", "--temp-project", @"C:\CustomTemp" };
        var config3 = ArgumentParser.Parse(args3);
        Assert.Equal(@"C:\CustomTemp", config3?.TempProjectPath);
    }

    [Fact]
    public void Parse_WithExplicitUnityVersion_ShouldParseCorrectly()
    {
        using var tempUnity = new TempUnityFile("TestUnity.exe");
        var args = new[] { tempUnity.Path, @"C:\Assets", "test", @"C:\Output", "--unity-version", "2022.3.35f1" };
        var config = ArgumentParser.Parse(args);
        
        Assert.NotNull(config);
        Assert.Equal(tempUnity.Path, config.UnityPath);
        Assert.Equal("2022.3.35f1", config.UnityVersion);
    }

    [Fact]
    public void Parse_TooFewArgs_ShouldReturnNull()
    {
        var args = new[] { "2022.3.35f1" };
        var config = ArgumentParser.Parse(args);
        Assert.Null(config);
    }

    [Fact]
    public void Parse_WithRelativePaths_ShouldConvertToAbsolute()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var args = new[] { "2022.3.35f1", "Assets", "test", "Output" };
        var config = ArgumentParser.Parse(args);
        
        Assert.NotNull(config);
        Assert.Equal(Path.GetFullPath(Path.Combine(currentDir, "Assets")), config.AssetDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(currentDir, "Output")), config.OutputDirectory);
    }
}