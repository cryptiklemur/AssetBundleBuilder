namespace CryptikLemur.AssetBundleBuilder;

public class BuildConfiguration {
    public string UnityPath { get; set; } = string.Empty;
    public string UnityVersion { get; set; } = string.Empty;
    public string AssetDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string BundleName { get; set; } = string.Empty;
    public string BuildTarget { get; set; } = ""; // Empty means current OS without platform suffix
    public string TempProjectPath { get; set; } = string.Empty;
    public bool CleanTempProject { get; set; } = false;
    public string LinkMethod { get; set; } = "copy"; // copy, symlink, hardlink, junction
    public string LogFile { get; set; } = string.Empty;
    public bool AutoInstallHub { get; set; } = false;
    public bool AutoInstallEditor { get; set; } = false;
    public bool CiMode { get; set; } = false;
    public VerbosityLevel Verbosity { get; set; } = VerbosityLevel.Normal;
    public bool NonInteractive { get; set; } = false;
    public List<string> ExcludePatterns { get; set; } = new();
    public List<string> IncludePatterns { get; set; } = new();
    public string ConfigFile { get; set; } = string.Empty;
    public string BundleConfigName { get; set; } = string.Empty;
}