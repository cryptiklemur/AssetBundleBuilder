namespace CryptikLemur.AssetBundleBuilder;

public class BuildConfiguration
{
    public string UnityPath { get; set; } = string.Empty;
    public string UnityVersion { get; set; } = string.Empty;
    public string AssetDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string BundleName { get; set; } = string.Empty;
    public string BuildTarget { get; set; } = "windows";
    public string TempProjectPath { get; set; } = string.Empty;
    public bool KeepTempProject { get; set; } = false;
    public bool CleanTempProject { get; set; } = false;
    public string LinkMethod { get; set; } = "copy"; // copy, symlink, hardlink, junction
}