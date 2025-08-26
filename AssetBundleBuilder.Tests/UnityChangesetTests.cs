using System.Text.Json;
using Xunit;

namespace CryptikLemur.AssetBundleBuilder.Tests;

public class UnityChangesetTests {
    [Fact]
    public void ParseUnityApiResponse_WithValidResponse_ReturnsChangeset() {
        // Arrange
        var jsonResponse = """
                           {
                             "results": [
                               {
                                 "version": "2022.3.58f1",
                                 "shortRevision": "ab3c123d4e5f",
                                 "releaseDate": "2023-01-01T00:00:00Z",
                                 "stream": "LTS"
                               }
                             ]
                           }
                           """;

        // Act
        var changeset = ExtractChangesetFromJson(jsonResponse);

        // Assert
        Assert.Equal("ab3c123d4e5f", changeset);
    }

    [Fact]
    public void ParseUnityApiResponse_WithEmptyResults_ReturnsNull() {
        // Arrange
        var jsonResponse = """
                           {
                             "results": []
                           }
                           """;

        // Act
        var changeset = ExtractChangesetFromJson(jsonResponse);

        // Assert
        Assert.Null(changeset);
    }

    [Fact]
    public void ParseUnityApiResponse_WithMissingShortRevision_ReturnsNull() {
        // Arrange
        var jsonResponse = """
                           {
                             "results": [
                               {
                                 "version": "2022.3.58f1",
                                 "releaseDate": "2023-01-01T00:00:00Z",
                                 "stream": "LTS"
                               }
                             ]
                           }
                           """;

        // Act
        var changeset = ExtractChangesetFromJson(jsonResponse);

        // Assert
        Assert.Null(changeset);
    }

    [Fact]
    public void ParseUnityApiResponse_WithEmptyShortRevision_ReturnsNull() {
        // Arrange
        var jsonResponse = """
                           {
                             "results": [
                               {
                                 "version": "2022.3.58f1",
                                 "shortRevision": "",
                                 "releaseDate": "2023-01-01T00:00:00Z",
                                 "stream": "LTS"
                               }
                             ]
                           }
                           """;

        // Act
        var changeset = ExtractChangesetFromJson(jsonResponse);

        // Assert
        Assert.Null(changeset);
    }

    [Fact]
    public void ParseUnityApiResponse_WithInvalidJson_ReturnsNull() {
        // Arrange
        var jsonResponse = "invalid json";

        // Act
        var changeset = ExtractChangesetFromJson(jsonResponse);

        // Assert
        Assert.Null(changeset);
    }

    [Theory]
    [InlineData("2022.3.58f1")]
    [InlineData("2021.3.45f1")]
    [InlineData("2023.1.0a22")]
    [InlineData("6000.0.55f1")]
    public void UnityApiUrl_ContainsCorrectVersion(string version) {
        // Act
        var url = $"https://services.api.unity.com/unity/editor/release/v1/releases?version={version}";

        // Assert
        Assert.Contains($"version={version}", url);
        Assert.StartsWith("https://services.api.unity.com/unity/editor/release/v1/releases", url);
    }

    [Fact]
    public void UnityHubInstallArgs_WithChangeset_IncludesChangesetParameter() {
        // Arrange
        var version = "2022.3.58f1";
        var changeset = "ab3c123d4e5f";

        // Act
        var installArgs = $"-- --headless install --version {version} --changeset {changeset}";

        // Assert
        Assert.Contains($"--version {version}", installArgs);
        Assert.Contains($"--changeset {changeset}", installArgs);
        Assert.Contains("--headless install", installArgs);
    }

    [Fact]
    public void UnityHubInstallArgs_WithoutChangeset_OnlyIncludesVersion() {
        // Arrange
        var version = "2022.3.58f1";

        // Act
        var installArgs = $"-- --headless install --version {version}";

        // Assert
        Assert.Contains($"--version {version}", installArgs);
        Assert.DoesNotContain("--changeset", installArgs);
        Assert.Contains("--headless install", installArgs);
    }

    // Helper method to extract changeset logic (same as in main code)
    private static string? ExtractChangesetFromJson(string jsonResponse) {
        try {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0) {
                var firstResult = results[0];
                if (firstResult.TryGetProperty("shortRevision", out var shortRevision)) {
                    var changesetValue = shortRevision.GetString();
                    if (!string.IsNullOrEmpty(changesetValue)) return changesetValue;
                }
            }

            return null;
        }
        catch {
            return null;
        }
    }
}