using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

public class TomlGlobalConfig {
    [TomlProperty("unity_version")] public string? UnityVersion { get; set; }
    [TomlProperty("unity_path")] public string? UnityPath { get; set; }
    [TomlProperty("asset_directory")] public string? AssetDirectory { get; set; }
    [TomlProperty("output_directory")] public string? OutputDirectory { get; set; }
    [TomlProperty("build_targets")] public List<string>? BuildTargets { get; set; }
    [TomlProperty("temp_project_path")] public string? TempProjectPath { get; set; }
    [TomlProperty("clean_temp_project")] public bool? CleanTempProject { get; set; }
    [TomlProperty("link_method")] public string? LinkMethod { get; set; }
    [TomlProperty("log_file")] public string? LogFile { get; set; }
    [TomlProperty("exclude_patterns")] public List<string>? ExcludePatterns { get; set; }
    [TomlProperty("include_patterns")] public List<string>? IncludePatterns { get; set; }
    [TomlProperty("filename")] public string? Filename { get; set; }
    [TomlProperty("targetless")] public bool? Targetless { get; set; }
}
