using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

public class TomlBundleConfig : TomlGlobalConfig {
    [TomlProperty("output_directory")] public string OutputDirectory { get; set; } = string.Empty;
    [TomlProperty("asset_directory")] public string AssetDirectory { get; set; } = string.Empty;
    [TomlProperty("bundle_name")] public string BundleName { get; set; } = string.Empty;
    [TomlProperty("description")] public string? Description { get; set; }
}