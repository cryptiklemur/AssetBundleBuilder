using System.Security.Cryptography;
using System.Text;

namespace CryptikLemur.AssetBundleBuilder.Utilities;

public static class HashUtility {
    public static string ComputeHash(string input) {
        ArgumentNullException.ThrowIfNull(input);

        using var sha256Hash = SHA256.Create();
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..8].ToLower();
    }
}