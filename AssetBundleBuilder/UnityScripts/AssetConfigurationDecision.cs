using System;

/// <summary>
///     Pure decision logic for whether an asset still needs import-setting configuration.
///     Kept free of UnityEditor types so it compiles both inside the temp Unity project and
///     in the main assembly where it is unit-tested.
/// </summary>
public static class AssetConfigurationDecision
{
    /// <summary>
    ///     Determines whether an asset should be (re)configured. An asset is considered already
    ///     configured only when it carries the target bundle name AND our platform override, which
    ///     is the state the labeler leaves it in. A bare <c>.meta</c> from Unity's initial import does
    ///     not count as configured, so first-run assets are still processed.
    /// </summary>
    public static bool ShouldConfigure(string existingBundleName, bool hasStandaloneOverride, string targetBundleName)
    {
        bool bundleNameMatches =
            string.Equals(existingBundleName, targetBundleName, StringComparison.OrdinalIgnoreCase);
        return !(bundleNameMatches && hasStandaloneOverride);
    }
}
