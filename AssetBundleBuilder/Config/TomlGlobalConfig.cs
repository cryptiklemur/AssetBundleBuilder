using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

public class TomlGlobalConfig : TomlSharedConfig {
    [TomlProperty("unity_version")] public string UnityVersion { get; set; } = string.Empty;
    [TomlProperty("unity_editor_path")] public string UnityEditorPath { get; set; } = string.Empty;
    [TomlProperty("unity_hub_path")] public string UnityHubPath { get; set; } = string.Empty;
    [TomlProperty("link_method")] public string LinkMethod { get; set; } = "copy";
}