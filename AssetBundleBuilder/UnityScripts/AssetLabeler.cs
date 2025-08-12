using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AssetLabeler
{
    private static readonly string assetsFolder = Path.Combine("Assets", "Data");

    /// <summary>
    ///     Converts a texture asset from Sprite to Default to prevent Unity from generating sprite sub-assets.
    /// </summary>
    /// <param name="assetPath">The path to the texture asset.</param>
    private static void ConvertSpriteToDefault(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter
            {
                textureType: TextureImporterType.Sprite
            } importer)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.SaveAndReimport();
        Console.WriteLine($"Converted {assetPath} from Sprite to Default.");
    }

    /// <summary>
    ///     Labels all assets  with a single common asset bundle name.
    /// </summary>
    /// <returns>The number of textures labeled.</returns>
    public static AssetBundleBuild LabelAllAssetsWithCommonName(string assetFileName)
    {
        var sourceDirectory = Path.Combine(assetsFolder, assetFileName);
        Debug.Log($"Finding all assets in {sourceDirectory}");
        
        // Ensure the directory exists before trying to enumerate files
        if (!Directory.Exists(sourceDirectory))
        {
            Debug.LogError($"Directory does not exist: {sourceDirectory}");
            Debug.LogError($"Current working directory: {Directory.GetCurrentDirectory()}");
            Debug.LogError($"Assets folder: {assetsFolder}");
            throw new DirectoryNotFoundException($"Could not find directory: {sourceDirectory}");
        }

        // Get all the files under modTexturesFolder (and its subdirectories).
        var filePaths = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
        var assetsLabeled = 0;

        var files = new List<string>();
        foreach (var filePath in filePaths)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var assetPath = Path.Combine("Assets", "Data", assetFileName, relativePath).Replace('\\', '/');
           
            // Skip if meta file already exists
            var metaPath = assetPath + ".meta";
            if (File.Exists(metaPath))
            {
                assetsLabeled++;
                files.Add(assetPath);
                continue; // Already imported/configured — leave it alone
            }
            
            // Normalize the path format.
            var extension = Path.GetExtension(assetPath).ToLower();
            if (extension == "") extension = ".shader";

            // Process only common texture and audio file types .
            if (extension != ".png" && extension != ".jpeg" && extension != ".jpg" && extension != ".psd" &&
                extension != ".wav" && extension != ".mp3" && extension != ".ogg" && extension != ".shader")
            {
                // Console.WriteLine("[Warning] Skipped asset, wrong format: " + filePath);
                continue;
            }

            var isTexture = extension is ".png" or ".jpeg" or ".jpg" or ".psd";
            var isPSD = extension is ".psd";
            var isAudio = extension is ".wav" or ".mp3" or ".ogg";

            // Confirm that the asset is located under the Assets folder.
            if (!assetPath.StartsWith("Assets")) continue;

            // Convert Sprite textures to Default to avoid additional sprite sub-assets.
            if (isTexture) ConvertSpriteToDefault(assetPath);
            if (isPSD) PSDMatteUtility.EnsureSettingsForFile(assetPath);

            // Set a common asset bundle name for every texture.
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                Debug.LogError($"[Warning] Could not get importer for: {assetPath}");
                continue;
            }
            importer.assetBundleName = assetFileName;

            if (isTexture && importer is TextureImporter textureImporter)
            {
                bool isTerrain = assetPath.ToLower().Contains("/terrain/");

                textureImporter.textureType = TextureImporterType.GUI;
                textureImporter.alphaIsTransparency = true;
                textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;

                // Check if the path has terrain in it, if so, set the wrap mode to Repeat.
                textureImporter.wrapMode = isTerrain ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                textureImporter.anisoLevel = isTerrain ? 8 : 1;

                textureImporter.filterMode = FilterMode.Trilinear;
                textureImporter.mipmapEnabled = true;
                textureImporter.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                textureImporter.sRGBTexture = !assetPath.ToLower().Contains("_mask") && !assetPath.ToLower().Contains("_normal");
                if (assetPath.ToLower().Contains("_normal")) textureImporter.textureType = TextureImporterType.NormalMap;

                textureImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Standalone",
                    overridden = true,
                    format = TextureImporterFormat.BC7,
                    maxTextureSize = 4096,
                    textureCompression = TextureImporterCompression.CompressedHQ
                });
            }
            else if (isAudio && importer is AudioImporter audioImporter)
            {
                audioImporter.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
                    loadType = AudioClipLoadType.CompressedInMemory,
                    quality = 0.25f,
                    preloadAudioData = true
                };
            }
            else if (importer is ShaderImporter shaderImporter)
            {
                // Don't need to do anything, I think
            }

            importer.SaveAndReimport();
            assetsLabeled++;
            files.Add(assetPath);
            //Console.WriteLine($"Labeled asset: {assetPath} as {assetFileName}");
        }

        Debug.Log($"Labeling complete: {assetsLabeled} assets labeled with \"{assetFileName}\".");
        return new AssetBundleBuild { assetBundleName = assetFileName, assetNames = files.ToArray() };
    }
}