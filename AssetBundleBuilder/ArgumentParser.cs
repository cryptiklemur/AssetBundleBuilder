namespace CryptikLemur.AssetBundleBuilder;

public static class ArgumentParser {
    public static BuildConfiguration? Parse(string[] args) {
        if (args.Length < 3) return null;

        var config = new BuildConfiguration();
        int argIndex;

        if (args[0] == "--unity-version" && args.Length >= 4) {
            config.UnityVersion = args[1];
            config.AssetDirectory = args[2];
            config.BundleName = args[3];

            // Optional output directory
            if (args.Length >= 5 && !args[4].StartsWith("--")) {
                config.OutputDirectory = args[4];
                argIndex = 5;
            }
            else {
                config.OutputDirectory = Directory.GetCurrentDirectory();
                argIndex = 4;
            }
        }
        else if (args.Length >= 3) {
            var firstArg = args[0];
            if (File.Exists(firstArg) && (firstArg.EndsWith("Unity.exe") || firstArg.EndsWith("Unity")))
                config.UnityPath = firstArg;
            else
                config.UnityVersion = firstArg;

            config.AssetDirectory = args[1];
            config.BundleName = args[2];

            // Optional output directory
            if (args.Length >= 4 && !args[3].StartsWith("--")) {
                config.OutputDirectory = args[3];
                argIndex = 4;
            }
            else {
                config.OutputDirectory = Directory.GetCurrentDirectory();
                argIndex = 3;
            }
        }
        else return null;

        for (var i = argIndex; i < args.Length; i++)
            switch (args[i]) {
                case "--bundle-name" when i + 1 < args.Length:
                    config.BundleName = args[++i];
                    break;
                case "--target" when i + 1 < args.Length:
                {
                    config.BuildTarget = args[++i];
                    if (config.BuildTarget != "windows" && config.BuildTarget != "mac" &&
                        config.BuildTarget != "linux")
                        return null;

                    break;
                }
                case "--temp-project" when i + 1 < args.Length:
                    config.TempProjectPath = Path.GetFullPath(args[++i]);
                    break;
                case "--unity-version" when i + 1 < args.Length:
                    config.UnityVersion = args[++i];
                    break;
                case "--keep-temp":
                    config.KeepTempProject = true;
                    break;
                case "--clean-temp":
                    config.CleanTempProject = true;
                    break;
                case "--copy":
                    config.LinkMethod = "copy";
                    break;
                case "--symlink":
                    config.LinkMethod = "symlink";
                    break;
                case "--hardlink":
                    config.LinkMethod = "hardlink";
                    break;
                case "--junction":
                    config.LinkMethod = "junction";
                    break;
                case "--logfile" when i + 1 < args.Length:
                    config.LogFile = Path.GetFullPath(args[++i]);
                    break;
                case "--ci":
                    config.CiMode = true;
                    break;
                case "--silent":
                    config.Silent = true;
                    break;
                case "--non-interactive":
                    config.NonInteractive = true;
                    break;
            }

        if (string.IsNullOrEmpty(config.BundleName)) return null;

        config.BundleName = config.BundleName.ToLower().Replace(" ", "");
        config.AssetDirectory = Path.GetFullPath(config.AssetDirectory);
        config.OutputDirectory = Path.GetFullPath(config.OutputDirectory);

        // Auto-detect CI environment
        if (!config.CiMode && Environment.GetEnvironmentVariable("CI") == "true") config.CiMode = true;

        return config;
    }
}