using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

public class TomlGlobalConfig {
    [TomlProperty("extends")] public string? Extends { get; set; }
    [TomlProperty("unity_version")] public string UnityVersion { get; set; } = string.Empty;
    [TomlProperty("unity_editor_path")] public string UnityEditorPath { get; set; } = string.Empty;
    [TomlProperty("unity_hub_path")] public string UnityHubPath { get; set; } = string.Empty;

    [TomlProperty("allowed_targets")] public List<string> AllowedTargets { get; set; } = ["windows", "mac", "linux"];
    [TomlProperty("temp_project_path")] public string? TempProjectPath { get; set; }
    [TomlProperty("clean_temp_project")] public bool CleanTempProject { get; set; } = false;
    [TomlProperty("link_method")] public string LinkMethod { get; set; } = "copy";
    [TomlProperty("log_file")] public string? LogFile { get; set; }
    [TomlProperty("exclude_patterns")] public List<string>? ExcludePatterns { get; set; }
    [TomlProperty("include_patterns")] public List<string>? IncludePatterns { get; set; }
    [TomlProperty("filename")] public string? Filename { get; set; }
    [TomlProperty("targetless")] public bool Targetless { get; set; } = true;
}