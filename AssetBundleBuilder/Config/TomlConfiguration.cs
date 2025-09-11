using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

/// <summary>
///     TOML configuration structure for loading .assetbundler.toml files
/// </summary>
public class TomlConfiguration {
    [TomlProperty("bundles")] public Dictionary<string, TomlBundleConfig> Bundles { get; set; } = [];
    [TomlProperty("global")] public TomlGlobalConfig Global { get; set; } = null!;
}