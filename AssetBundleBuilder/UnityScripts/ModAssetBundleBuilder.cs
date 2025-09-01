using System.Collections.Generic;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;

public class ModAssetBundleBuilder
{
    private const string outputDirectoryRoot = "Assets/AssetBundles";
    private static Dictionary<string, BuildTarget> supportedTargets = new Dictionary<string, BuildTarget>()
    {
        { "windows", BuildTarget.StandaloneWindows64},
        { "mac", BuildTarget.StandaloneOSX},
        { "linux", BuildTarget.StandaloneLinux64},
    };

    // Bundle configuration structure for multiple bundles
    [Serializable]
    public class BundleConfig
    {
        public string bundleName;
        public string assetDirectory;
        public string outputDirectory;
        public List<string> buildTargets; // null for targetless, array for targeted bundles
        public bool noPlatformSuffix;
        public string filenameFormat;
        public List<string> includePatterns;
        public List<string> excludePatterns;
        
        public BundleConfig()
        {
            includePatterns = new List<string>();
            excludePatterns = new List<string>();
        }
    }
    
    [Serializable]
    public class MultiBundleConfig
    {
        public List<BundleConfig> bundles;
        
        public MultiBundleConfig()
        {
            bundles = new List<BundleConfig>();
        }
    }

    [MenuItem("Assets/Build Asset Bundle")]
    public static void BuildBundles()
    {
        Debug.Log($"Starting AssetBundle Builder");
        var arguments = Environment.GetCommandLineArgs();
        
        // Find the bundle config file
        string bundleConfigFile = null;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == "-bundleConfigFile" && i + 1 < arguments.Length)
            {
                bundleConfigFile = arguments[i + 1];
                Debug.Log($"Using bundle config file: {bundleConfigFile}");
                break;
            }
        }
        
        if (string.IsNullOrEmpty(bundleConfigFile))
        {
            throw new Exception("Bundle config file is required. Use -bundleConfigFile parameter.");
        }
        
        if (!File.Exists(bundleConfigFile))
        {
            throw new Exception($"Bundle config file not found: {bundleConfigFile}");
        }
        
        // Build bundles from JSON config
        BuildBundlesFromConfig(bundleConfigFile);
    }
    
    private static void BuildBundlesFromConfig(string configFile)
    {
        
        var json = File.ReadAllText(configFile);
        var config = JsonUtility.FromJson<MultiBundleConfig>(json);
        
        if (config == null || config.bundles == null || config.bundles.Count == 0)
        {
            throw new Exception("No bundles defined in config file");
        }
        
        Debug.Log($"Building {config.bundles.Count} bundles");
        
        int successCount = 0;
        
        int failCount = 0;
        var failedBundles = new List<string>();
        
        foreach (var bundleConfig in config.bundles)
        {
            try
            {
                Debug.Log($"Building bundle: {bundleConfig.bundleName}");
                BuildBundle(bundleConfig);
                successCount++;
                Debug.Log($"Successfully built bundle: {bundleConfig.bundleName}");
            }
            catch (Exception ex)
            {
                failCount++;
                failedBundles.Add(bundleConfig.bundleName);
                Debug.LogError($"Failed to build bundle {bundleConfig.bundleName}: {ex.Message}");
                // Continue with other bundles instead of stopping
            }
        }
        
        Debug.Log($"Bundle build complete. Success: {successCount}, Failed: {failCount}");
        if (failedBundles.Count > 0)
        {
            Debug.LogError($"Failed bundles: {string.Join(", ", failedBundles)}");
            // Throw at the end so Unity returns non-zero exit code
            throw new Exception($"Failed to build {failCount} bundle(s): {string.Join(", ", failedBundles)}");
        }
    }
    

    private static void BuildBundle(BundleConfig config)
    {
        var assetBundleName = config.bundleName;

        // Assets should already be linked/copied by the main AssetBundleBuilder before Unity starts
        // Just verify the expected path exists (note: path is not necessarily named after the bundle anymore)
        var expectedPaths = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Data", assetBundleName),
            // Also check for source-based directory names
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Data")
        };
        
        var foundPath = expectedPaths.FirstOrDefault(Directory.Exists);
        if (foundPath == null) {
            throw new Exception($"Expected assets in one of: {string.Join(", ", expectedPaths)}. See the readme.");
        }

        // Ensure textures are labeled correctly before proceeding.
        var bundle = AssetLabeler.LabelAllAssetsWithCommonName(assetBundleName, config.includePatterns, config.excludePatterns);
        if (bundle.assetNames == null || bundle.assetNames.Count() == 0) {
            throw new Exception("No assets were labeled; aborting asset bundle build.");
        }

        // Build to a local AssetBundles directory in the temp project for caching
        var tempOutputLocation = Path.Combine(Directory.GetCurrentDirectory(), "AssetBundles");
        
        // Final output location where we'll copy the bundles
        var finalOutputLocation = string.IsNullOrEmpty(config.outputDirectory) 
            ? Path.Combine(config.assetDirectory, "AssetBundles") 
            : config.outputDirectory;

        Debug.Log($"Building {assetBundleName} asset bundle in temp location: {tempOutputLocation}");
        Debug.Log($"Final output will be copied to: {finalOutputLocation}");
        
        // Handle targetless bundles (buildTargets is null)
        if (config.buildTargets == null || config.buildTargets.Count() == 0)
        {
            Debug.Log("Building targetless bundle");
            BuildBundleForTarget(config, bundle, assetBundleName, tempOutputLocation, finalOutputLocation, null);
        }
        else
        {
            // Build for each target in the array
            Debug.Log($"Building bundle for {config.buildTargets.Count} targets: {string.Join(", ", config.buildTargets)}");
            foreach (var target in config.buildTargets)
            {
                BuildBundleForTarget(config, bundle, assetBundleName, tempOutputLocation, finalOutputLocation, target);
            }
        }
    }
    
    private static void BuildBundleForTarget(BundleConfig config, AssetBundleBuild bundle, string assetBundleName, string tempOutputLocation, string finalOutputLocation, string buildTarget)
    {
        // Determine target platform
        BuildTarget targetPlatform;
        bool isTargetless = buildTarget == null;
        
        if (isTargetless)
        {
            // For targetless bundles, use the current platform but don't add suffix
            targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            Debug.Log($"Building targetless bundle using current platform: {targetPlatform}");
        }
        else if (supportedTargets.ContainsKey(buildTarget))
        {
            targetPlatform = supportedTargets[buildTarget];
            Debug.Log($"Building for target: {buildTarget} = {targetPlatform}");
        }
        else
        {
            throw new Exception($"Unsupported build target: {buildTarget}. Supported targets: {string.Join(", ", supportedTargets.Keys)}");
        }
        
        // Switch Unity to the target platform if needed (skip for targetless)
        if (!isTargetless)
        {
            var currentGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var currentTarget = EditorUserBuildSettings.activeBuildTarget;
            
            var targetGroup = BuildPipeline.GetBuildTargetGroup(targetPlatform);
            if (currentTarget != targetPlatform)
            {
                Debug.Log($"Switching from {currentTarget} to {targetPlatform}");
                EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, targetPlatform);
                Debug.Log($"Successfully switched to target: {targetPlatform}");
            }
            else
            {
                Debug.Log($"Already building for target: {targetPlatform}");
            }
        }
        
        // Clean and create temp output directory
        if (Directory.Exists(tempOutputLocation)) Directory.Delete(tempOutputLocation, true);
        if (!Directory.Exists(tempOutputLocation)) Directory.CreateDirectory(tempOutputLocation);

        // Build the bundle to temp location
        var bundles = new AssetBundleBuild[1];
        bundles[0] = bundle;

        Debug.Log($"Building bundle for target: {buildTarget ?? "targetless"}");
        var manifest = BuildPipeline.BuildAssetBundles(
            tempOutputLocation,
            bundles,
            BuildAssetBundleOptions.ChunkBasedCompression,
            targetPlatform
        );
        
        if (manifest != null)
        {
            foreach (var bn in manifest.GetAllAssetBundles())
            {
                string projectRelativePath = tempOutputLocation + "/" + bn;
                Debug.Log($"Size of AssetBundle {projectRelativePath} is {new FileInfo(projectRelativePath).Length}");
            }
        } else {
            throw new Exception($"Failed to build asset bundle for target {buildTarget ?? "targetless"}");
        }

        // Clean up Unity-generated files we don't need
        var unityManifestFile = Path.Combine(tempOutputLocation, "AssetBundles");
        var unityManifestMetaFile = Path.Combine(tempOutputLocation, "AssetBundles.manifest");
        if (File.Exists(unityManifestFile)) File.Delete(unityManifestFile);
        if (File.Exists(unityManifestMetaFile)) File.Delete(unityManifestMetaFile);

        // Determine the final filename using custom format or default
        string finalFileName;
        bool noPlatformSuffix = isTargetless || config.noPlatformSuffix;
        
        if (!string.IsNullOrEmpty(config.filenameFormat))
        {
            // Use custom filename format with variable substitution
            finalFileName = ApplyFilenameFormat(config.filenameFormat, assetBundleName, buildTarget ?? "none", noPlatformSuffix);
        }
        else
        {
            // Use default naming format
            var normalizedBundleName = assetBundleName.Replace(".", "_");
            
            // Map build target to short suffix
            string platformSuffix = (buildTarget ?? "none") switch
            {
                "windows" => "win",
                "mac" => "mac",
                "linux" => "linux",
                _ => buildTarget ?? "none"
            };
            
            finalFileName = noPlatformSuffix 
                ? $"resource_{normalizedBundleName}"
                : $"resource_{normalizedBundleName}_{platformSuffix}";
        }
        
        Debug.Log($"Bundle naming: '{assetBundleName}' -> '{finalFileName}' (no platform suffix: {noPlatformSuffix})");
        
        var originalBundleFile = Path.Combine(tempOutputLocation, assetBundleName);
        var originalManifestFile = Path.Combine(tempOutputLocation, assetBundleName + ".manifest");

        // Copy the built files to final output location with new naming
        if (!Directory.Exists(finalOutputLocation)) Directory.CreateDirectory(finalOutputLocation);
        
        var finalBundleFile = Path.Combine(finalOutputLocation, finalFileName);
        var finalManifestFile = Path.Combine(finalOutputLocation, finalFileName + ".manifest");
        
        Debug.Log($"Original bundle file: {originalBundleFile}");
        Debug.Log($"Copying bundle to final location: {finalBundleFile}");
        File.Copy(originalBundleFile, finalBundleFile, true);
        File.Copy(originalManifestFile, finalManifestFile, true);
        
        Debug.Log($"Successfully built and copied bundle for target: {buildTarget ?? "targetless"}");
    }

    private static string ApplyFilenameFormat(string format, string bundleName, string buildTarget, bool noPlatformSuffix)
    {
        // Normalize the bundle name (replace dots with underscores)
        var normalizedBundleName = bundleName.Replace(".", "_");
        
        // Map build target to short suffix
        string platformSuffix = buildTarget switch
        {
            "windows" => "win",
            "mac" => "mac",
            "linux" => "linux",
            _ => buildTarget
        };
        
        // Replace variables in the format string
        var result = format;
        result = result.Replace("[bundle_name]", normalizedBundleName);
        result = result.Replace("[bundlename]", normalizedBundleName); // Allow both styles
        result = result.Replace("[bundle-name]", normalizedBundleName); // Allow hyphenated style
        
        // Only include platform if we should have a platform suffix
        if (!noPlatformSuffix)
        {
            result = result.Replace("[platform]", platformSuffix);
            result = result.Replace("[target]", platformSuffix); // Allow [target] as alias
        }
        else
        {
            // Remove platform variables if no platform suffix
            result = result.Replace("_[platform]", "");
            result = result.Replace("[platform]", "");
            result = result.Replace("_[target]", "");
            result = result.Replace("[target]", "");
        }
        
        // Also support the original bundle name with dots
        result = result.Replace("[original_bundle_name]", bundleName);
        
        return result;
    }

}