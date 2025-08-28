using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

public class TomlBundleConfig : TomlGlobalConfig {
    [TomlProperty("asset_directory")] public string? AssetDirectory { get; set; }
    [TomlProperty("bundle_name")] public string? BundleName { get; set; }
    [TomlProperty("description")] public string? Description { get; set; }
}