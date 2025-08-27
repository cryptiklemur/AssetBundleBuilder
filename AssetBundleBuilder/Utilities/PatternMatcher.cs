using System.Text.RegularExpressions;

namespace CryptikLemur.AssetBundleBuilder.Utilities;

public static class PatternMatcher {
    public static bool IsExcluded(string relativePath, List<string>? excludePatterns) {
        if (excludePatterns == null || excludePatterns.Count == 0)
            return false;

        // Normalize path separators for consistent matching
        var normalizedPath = relativePath.Replace('\\', '/');

        foreach (var pattern in excludePatterns) {
            // Convert glob pattern to regex
            var regexPattern = GlobToRegex(pattern);
            if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase)) return true;
        }

        return false;
    }

    public static bool IsIncluded(string relativePath, List<string>? includePatterns) {
        if (includePatterns == null || includePatterns.Count == 0)
            return true;

        // Normalize path separators for consistent matching
        var normalizedPath = relativePath.Replace('\\', '/');

        foreach (var pattern in includePatterns) {
            // Convert glob pattern to regex
            var regexPattern = GlobToRegex(pattern);
            if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase)) return true;
        }

        return false;
    }

    public static string GlobToRegex(string glob) {
        // Normalize path separators
        glob = glob.Replace('\\', '/');

        // Handle special case of **/ - matches any number of directories (including zero)
        if (glob.StartsWith("**/")) {
            var remainder = glob.Substring(3);
            var escapedRemainder = Regex.Escape(remainder)
                .Replace("\\*", "[^/]*") // * matches any character except /
                .Replace("\\?", "."); // ? matches single character

            // **/ allows the file to be at root or any depth
            return "^(.*/)?" + escapedRemainder + "$";
        }

        // Escape regex special characters except * and ?
        var escaped = Regex.Escape(glob);

        // Handle ** before * to avoid incorrect replacements
        escaped = escaped.Replace("\\*\\*", ".*"); // ** matches anything
        escaped = escaped.Replace("\\*", "[^/]*"); // * matches any character except /
        escaped = escaped.Replace("\\?", "."); // ? matches single character

        // If pattern doesn't start with /, it can match at any depth
        if (!glob.StartsWith("/"))
            escaped = "(^|.*/)" + escaped;

        // If pattern ends with /, match directories
        if (glob.EndsWith("/"))
            escaped = escaped + ".*";

        return "^" + escaped + "$";
    }
}