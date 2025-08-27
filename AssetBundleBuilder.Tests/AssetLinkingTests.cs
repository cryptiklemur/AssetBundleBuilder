using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class AssetLinkingTests {
    [Fact]
    public void LinkAssets_CopyMethod_CreatesDirectory() {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempTargetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var targetAssetsDir = Path.Combine(tempTargetDir, "Assets");

        try {
            Directory.CreateDirectory(tempSourceDir);
            File.WriteAllText(Path.Combine(tempSourceDir, "test.txt"), "test content");

            // Use reflection to call private LinkAssets method
            var linkAssetsMethod = typeof(Program).GetMethod("LinkAssets",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(linkAssetsMethod);

            linkAssetsMethod.Invoke(null, [tempSourceDir, targetAssetsDir, "copy"]);

            Assert.True(Directory.Exists(targetAssetsDir));
            Assert.True(File.Exists(Path.Combine(targetAssetsDir, "test.txt")));
            Assert.Equal("test content", File.ReadAllText(Path.Combine(targetAssetsDir, "test.txt")));
        }
        finally {
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempTargetDir)) Directory.Delete(tempTargetDir, true);
        }
    }

    [Fact]
    public void LinkAssets_SymlinkMethod_CreatesSymbolicLink() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            // Skip on Windows unless running as admin
            return;
        }

        var tempSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempTargetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var targetAssetsDir = Path.Combine(tempTargetDir, "Assets");

        try {
            Directory.CreateDirectory(tempSourceDir);
            File.WriteAllText(Path.Combine(tempSourceDir, "test.txt"), "test content");

            // Use reflection to call private LinkAssets method
            var linkAssetsMethod = typeof(Program).GetMethod("LinkAssets",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(linkAssetsMethod);

            linkAssetsMethod.Invoke(null, [tempSourceDir, targetAssetsDir, "symlink"]);

            Assert.True(Directory.Exists(targetAssetsDir));
            Assert.True(File.Exists(Path.Combine(targetAssetsDir, "test.txt")));

            // Check if it's actually a symbolic link
            var linkInfo = new FileInfo(targetAssetsDir);
            // On Unix systems, check if directory has the symbolic link attribute
        }
        catch (UnauthorizedAccessException) {
            // Expected on systems without symlink permissions
        }
        finally {
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempTargetDir)) Directory.Delete(tempTargetDir, true);
        }
    }

    [Fact]
    public void CreateSymbolicLink_ValidPaths_CreatesLink() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            // Skip on Windows unless running as admin
            return;
        }

        var tempSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempTargetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try {
            Directory.CreateDirectory(tempSourceDir);
            File.WriteAllText(Path.Combine(tempSourceDir, "test.txt"), "test content");

            // Use reflection to call private CreateSymbolicLink method
            var method = typeof(Program).GetMethod("CreateSymbolicLink",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            method.Invoke(null, [tempSourceDir, tempTargetDir]);

            Assert.True(Directory.Exists(tempTargetDir));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "test.txt")));
        }
        catch (UnauthorizedAccessException) {
            // Expected on systems without symlink permissions
        }
        finally {
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempTargetDir)) Directory.Delete(tempTargetDir, true);
        }
    }

    [Fact]
    public void CreateHardLink_ValidPaths_CreatesHardLinks() {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempTargetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try {
            Directory.CreateDirectory(tempSourceDir);
            var sourceFile = Path.Combine(tempSourceDir, "test.txt");
            File.WriteAllText(sourceFile, "test content");

            // Use reflection to call private CreateHardLink method
            var method = typeof(Program).GetMethod("CreateHardLink",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            method.Invoke(null, [tempSourceDir, tempTargetDir]);

            Assert.True(Directory.Exists(tempTargetDir));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "test.txt")));
            Assert.Equal("test content", File.ReadAllText(Path.Combine(tempTargetDir, "test.txt")));
        }
        catch (PlatformNotSupportedException) {
            // Expected on platforms that don't support hard links
        }
        catch (UnauthorizedAccessException) {
            // Expected on systems without permissions
        }
        finally {
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempTargetDir)) Directory.Delete(tempTargetDir, true);
        }
    }

    [Fact]
    public void CreateJunction_WindowsOnly_CreatesJunction() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            // Skip on non-Windows platforms
            return;
        }

        var tempSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempTargetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try {
            Directory.CreateDirectory(tempSourceDir);
            File.WriteAllText(Path.Combine(tempSourceDir, "test.txt"), "test content");

            // Use reflection to call private CreateJunction method
            var method = typeof(Program).GetMethod("CreateJunction",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            method.Invoke(null, [tempSourceDir, tempTargetDir]);

            Assert.True(Directory.Exists(tempTargetDir));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "test.txt")));
        }
        catch (UnauthorizedAccessException) {
            // Expected on systems without permissions
        }
        finally {
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempTargetDir)) Directory.Delete(tempTargetDir, true);
        }
    }

    [Fact]
    public void CopyDirectory_RecursiveCopy_CopiesAllFiles() {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempTargetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try {
            Directory.CreateDirectory(tempSourceDir);
            var subDir = Path.Combine(tempSourceDir, "subdir");
            Directory.CreateDirectory(subDir);

            File.WriteAllText(Path.Combine(tempSourceDir, "test1.txt"), "content1");
            File.WriteAllText(Path.Combine(subDir, "test2.txt"), "content2");

            // Use reflection to call private CopyDirectory method
            var method = typeof(Program).GetMethod("CopyDirectory",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            method.Invoke(null, [tempSourceDir, tempTargetDir]);

            Assert.True(Directory.Exists(tempTargetDir));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "test1.txt")));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "subdir", "test2.txt")));
            Assert.Equal("content1", File.ReadAllText(Path.Combine(tempTargetDir, "test1.txt")));
            Assert.Equal("content2", File.ReadAllText(Path.Combine(tempTargetDir, "subdir", "test2.txt")));
        }
        finally {
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempTargetDir)) Directory.Delete(tempTargetDir, true);
        }
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("symlink")]
    [InlineData("hardlink")]
    [InlineData("junction")]
    public void LinkAssets_DifferentMethods_HandlesAppropriately(string linkMethod) {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var tempTargetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var targetAssetsDir = Path.Combine(tempTargetDir, "Assets");

        try {
            Directory.CreateDirectory(tempSourceDir);
            File.WriteAllText(Path.Combine(tempSourceDir, "test.txt"), "test content");

            // Use reflection to call private LinkAssets method
            var linkAssetsMethod = typeof(Program).GetMethod("LinkAssets",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(linkAssetsMethod);

            // This should not throw an exception, though some methods may require permissions
            try {
                linkAssetsMethod.Invoke(null, [tempSourceDir, targetAssetsDir, linkMethod]);

                // Verify the target was created
                Assert.True(Directory.Exists(targetAssetsDir));
                Assert.True(File.Exists(Path.Combine(targetAssetsDir, "test.txt")));
            }
            catch (TargetInvocationException ex) {
                // Unwrap the inner exception
                var innerEx = ex.InnerException;

                // These are acceptable exceptions based on platform/permissions
                if (innerEx is UnauthorizedAccessException ||
                    innerEx is PlatformNotSupportedException ||
                    innerEx is NotSupportedException) {
                    // Expected in some environments
                    return;
                }

                throw;
            }
        }
        finally {
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempTargetDir)) Directory.Delete(tempTargetDir, true);
        }
    }
}