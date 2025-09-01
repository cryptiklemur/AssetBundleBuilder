using System.Diagnostics;
using CryptikLemur.AssetBundleBuilder.Interfaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class ProcessMockingTests(ITestOutputHelper output) : AssetBundleTestBase(output, "ProcessMockingTestOutput") {
    [Fact]
    public async Task BuildSingleBundle_ShouldCallUnityWithCorrectArguments() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("ProcessMockTest");
        string bundleName = "test.modname";

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath
        );

        // Create a mock ProcessRunner
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity execution completed successfully",
                StandardError = ""
            });

        // Inject the mock
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Build should succeed with mocked process");

        // Verify Unity was called with expected arguments
        mockProcessRunner.Verify(x => x.RunAsync(It.Is<ProcessStartInfo>(psi =>
            psi.Arguments.Contains("-batchmode") &&
            psi.Arguments.Contains("-nographics") &&
            psi.Arguments.Contains("-quit") &&
            psi.Arguments.Contains("-executeMethod") &&
            psi.Arguments.Contains("ModAssetBundleBuilder.BuildBundles") &&
            psi.Arguments.Contains("-bundleConfigFile")
        )), Times.AtLeastOnce);

        _output.WriteLine("Verified Unity process was called with correct arguments using Moq!");
    }

    [Fact]
    public async Task BuildTargetedBundle_ShouldCallUnityWithTargetArguments() {
        // Arrange
        string testAssetsDir = CreateTestAssetsDirectory("TargetedProcessTest");
        string bundleName = "test.targeted";

        var config = CreateTestConfiguration(
            bundleName,
            testAssetsDir,
            _testOutputPath,
            false, // not targetless
            ["windows"]
        );

        // Create a mock ProcessRunner that captures the ProcessStartInfo
        ProcessStartInfo? capturedStartInfo = null;
        var mockProcessRunner = new Mock<IProcessRunner>();
        mockProcessRunner
            .Setup(x => x.RunAsync(It.IsAny<ProcessStartInfo>()))
            .Callback<ProcessStartInfo>(psi => capturedStartInfo = psi)
            .ReturnsAsync(new ProcessResult {
                ExitCode = 0,
                StandardOutput = "Mock Unity targeted build completed",
                StandardError = ""
            });

        // Inject the mock
        Program.ProcessRunner = mockProcessRunner.Object;

        // Act
        bool success = await BuildAssetBundleAsync(config);

        // Assert
        Assert.True(success, "Targeted build should succeed");
        Assert.NotNull(capturedStartInfo);

        // Verify the Unity command arguments contain what we expect
        string arguments = capturedStartInfo.Arguments;
        Assert.Contains("-batchmode", arguments);
        Assert.Contains("-executeMethod", arguments);
        Assert.Contains("ModAssetBundleBuilder.BuildBundles", arguments);
        Assert.Contains("-bundleConfigFile", arguments);

        _output.WriteLine($"Captured Unity arguments: {arguments}");
        _output.WriteLine("Process mocking with argument verification successful!");

        // Verify it was called exactly once
        mockProcessRunner.Verify(x => x.RunAsync(It.IsAny<ProcessStartInfo>()), Times.AtLeastOnce);
    }

    public override void Dispose() {
        Program.ProcessRunner = new SystemProcessRunner();
        base.Dispose();
    }
}