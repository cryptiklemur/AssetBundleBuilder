using System.Collections.Generic;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Xml.Linq;

public class ModAssetBundleBuilder
{
    private const string outputDirectoryRoot = "Assets/AssetBundles";
    private static Dictionary<string, BuildTarget> supportedTargets = new Dictionary<string, BuildTarget>()
    {
        { "windows", BuildTarget.StandaloneWindows64},
        { "mac", BuildTarget.StandaloneOSX},
        { "linux", BuildTarget.StandaloneLinux64},
    };

    [MenuItem("Assets/Build Asset Bundle")]
    public static void BuildBundles()
    {
        Debug.Log($"Starting AssetBundle Builder");
        var arguments = Environment.GetCommandLineArgs();
        var bundleName = "";
        var outputDirectory = "";
        var assetDirectory = "";
        var buildTarget = "all";
        for (int i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];
            
            if (arg == "-buildTarget" && i + 1 < arguments.Length)
            {
                buildTarget = arguments[i + 1];
                
                // Map Unity command line build targets to our simplified names
                if (buildTarget == "StandaloneWindows64" || buildTarget == "Win64")
                    buildTarget = "windows";
                else if (buildTarget == "StandaloneOSX" || buildTarget == "OSXUniversal")
                    buildTarget = "mac";
                else if (buildTarget == "StandaloneLinux64" || buildTarget == "Linux64")
                    buildTarget = "linux";
                
                Debug.Log($"Using build target: {buildTarget}");
                i++; // Skip the next argument since we've consumed it
            }
            else if (arg == "-bundleName" && i + 1 < arguments.Length)
            {
                bundleName = arguments[i + 1];
                Debug.Log($"Using bundle name: {bundleName}");
                i++; // Skip the next argument since we've consumed it
            }
            else if (arg == "-output" && i + 1 < arguments.Length)
            {
                outputDirectory = arguments[i + 1];
                if (!outputDirectory.StartsWith("/"))
                {
                    outputDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputDirectory));
                }
                Debug.Log($"Using output directory: {outputDirectory}");
                i++; // Skip the next argument since we've consumed it
            }
            else if (arg == "-assetDirectory" && i + 1 < arguments.Length)
            {
                assetDirectory = arguments[i + 1];
                if (!assetDirectory.StartsWith("/"))
                {
                    assetDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetDirectory));
                }
                Debug.Log($"Using asset directory: {assetDirectory}");
                i++; // Skip the next argument since we've consumed it
            }
        }

        if (string.IsNullOrEmpty(bundleName))
        {
            throw new Exception("Bundle name must be set with -bundleName.");
        }

        var assetBundleName = bundleName;

        // Assets should already be linked/copied by the main AssetBundleBuilder before Unity starts
        // Just verify the expected path exists

        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Data", assetBundleName);
        if (!Directory.Exists(expectedPath)) {
            throw new Exception($"Expected assets in {expectedPath}. See the readme.");
        }

        // Ensure textures are labeled correctly before proceeding.
        var bundle = AssetLabeler.LabelAllAssetsWithCommonName(assetBundleName);
        if (bundle.assetNames == null || bundle.assetNames.Count() == 0) {
            throw new Exception("No assets were labeled; aborting asset bundle build.");
        }

        // Build to a local AssetBundles directory in the temp project for caching
        var tempOutputLocation = Path.Combine(Directory.GetCurrentDirectory(), "AssetBundles");
        
        // Final output location where we'll copy the bundles
        var finalOutputLocation = string.IsNullOrEmpty(outputDirectory) 
            ? Path.Combine(assetDirectory, "AssetBundles") 
            : outputDirectory;

        // Since the bundle only includes generic assets like textures or sounds
        // and not platform-specific assets, we can build for all platforms.
        Debug.Log($"Building asset bundle in temp location: {tempOutputLocation}");
        Debug.Log($"Final output will be copied to: {finalOutputLocation}");

        // Build the asset bundle for the specified target with LZ4 compression.
        if (!supportedTargets.ContainsKey(buildTarget))
        {
            throw new Exception($"Unsupported build target: {buildTarget}. Supported targets: {string.Join(", ", supportedTargets.Keys)}");
        }
        
        var targetPlatform = supportedTargets[buildTarget];
        Debug.Log($"Building for target: {buildTarget} = {targetPlatform}");
        
        // Clean and create temp output directory
        if (Directory.Exists(tempOutputLocation)) Directory.Delete(tempOutputLocation, true);
        if (!Directory.Exists(tempOutputLocation)) Directory.CreateDirectory(tempOutputLocation);

        // Build the bundle to temp location
        var bundles = new AssetBundleBuild[1];
        bundles[0] = bundle;

        Debug.Log($"Building bundle for target: {buildTarget}");
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
            throw new Exception($"Failed to build asset bundle for target {buildTarget}");
        }

        // Clean up Unity-generated files we don't need
        var unityManifestFile = Path.Combine(tempOutputLocation, "AssetBundles");
        var unityManifestMetaFile = Path.Combine(tempOutputLocation, "AssetBundles.manifest");
        if (File.Exists(unityManifestFile)) File.Delete(unityManifestFile);
        if (File.Exists(unityManifestMetaFile)) File.Delete(unityManifestMetaFile);

        // Generate the new naming format: resource_<bundlename>_<target>
        // Convert periods to underscores in bundle name
        var normalizedBundleName = assetBundleName.Replace(".", "_");
        var finalFileName = $"resource_{normalizedBundleName}_{buildTarget}";
        
        Debug.Log($"Bundle naming: '{assetBundleName}' -> '{finalFileName}'");
        
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
        
        Debug.Log($"Successfully built and copied bundle for target: {buildTarget}");

        Debug.Log("Asset bundles built successfully.");
    }


}