using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

public class TomlBundleConfig : TomlSharedConfig {
    [TomlProperty("bundle_name")] public string BundleName { get; set; } = string.Empty;
    [TomlProperty("bundle_path")] public string BundlePath { get; set; } = string.Empty;
    [TomlProperty("description")] public string Description { get; set; } = string.Empty;
}