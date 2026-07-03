using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class AssetConfigurationDecisionTests {
    [Fact]
    public void ShouldConfigure_WhenNeverConfigured_ReturnsTrue() {
        bool result = AssetConfigurationDecision.ShouldConfigure(
            existingBundleName: "",
            hasStandaloneOverride: false,
            targetBundleName: "author.name");

        Assert.True(result);
    }

    [Fact]
    public void ShouldConfigure_WhenAlreadyOursAndOverridePresent_ReturnsFalse() {
        bool result = AssetConfigurationDecision.ShouldConfigure(
            existingBundleName: "author.name",
            hasStandaloneOverride: true,
            targetBundleName: "author.name");

        Assert.False(result);
    }

    [Fact]
    public void ShouldConfigure_WhenBundleNameMatchesButNoOverride_ReturnsTrue() {
        bool result = AssetConfigurationDecision.ShouldConfigure(
            existingBundleName: "author.name",
            hasStandaloneOverride: false,
            targetBundleName: "author.name");

        Assert.True(result);
    }

    [Fact]
    public void ShouldConfigure_WhenOverridePresentButDifferentBundleName_ReturnsTrue() {
        bool result = AssetConfigurationDecision.ShouldConfigure(
            existingBundleName: "someone.else",
            hasStandaloneOverride: true,
            targetBundleName: "author.name");

        Assert.True(result);
    }

    [Fact]
    public void ShouldConfigure_TreatsBundleNameCaseInsensitively() {
        bool result = AssetConfigurationDecision.ShouldConfigure(
            existingBundleName: "Author.Name",
            hasStandaloneOverride: true,
            targetBundleName: "author.name");

        Assert.False(result);
    }
}
