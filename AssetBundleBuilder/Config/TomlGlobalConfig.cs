using Tomlet.Attributes;

namespace CryptikLemur.AssetBundleBuilder.Config;

public class TomlGlobalConfig {
    [TomlProperty("unity_version")] public string? UnityVersion { get; set; }
    [TomlProperty("unity_path")] public string? UnityPath { get; set; }
    [TomlProperty("output_directory")] public string? OutputDirectory { get; set; }
    [TomlProperty("build_target")] public string? BuildTarget { get; set; }
    [TomlProperty("temp_project_path")] public string? TempProjectPath { get; set; }
    [TomlProperty("clean_temp_project")] public bool? CleanTempProject { get; set; }
    [TomlProperty("link_method")] public string? LinkMethod { get; set; }
    [TomlProperty("log_file")] public string? LogFile { get; set; }
    [TomlProperty("ci_mode")] public bool? CiMode { get; set; }
    [TomlProperty("verbosity")] public string? Verbosity { get; set; }
    [TomlProperty("non_interactive")] public bool? NonInteractive { get; set; }
    [TomlProperty("exclude_patterns")] public List<string>? ExcludePatterns { get; set; }
    [TomlProperty("include_patterns")] public List<string>? IncludePatterns { get; set; }
    [TomlProperty("filename")] public string? Filename { get; set; }
}