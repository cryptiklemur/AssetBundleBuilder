using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class AssetLabeler
{
    private static readonly string assetsFolder = Path.Combine("Assets", "Data");

    /// <summary>
    /// Custom texture type enum that matches Unity's TextureImporterType values plus custom Mask type
    /// </summary>
    public enum CustomTextureImporterType
    {
        Default,
        NormalMap,
        Gui,
        Sprite,
        Cursor,
        Cookie,
        Lightmap,
        SingleChannel,
        Shadowmask,
        DirectionalLightmap,
        Mask, // Custom type for mask textures
        Terrain // Custom type for terrain textures
        
        static TextureImporterType ToTextureImporterType() {
                return typeName switch {
                    NormalMap => CustomTextureImporterType.NormalMap,
                    Gui => CustomTextureImporterType.Gui,
                    Sprite => CustomTextureImporterType.Sprite,
                    Cursor => CustomTextureImporterType.Cursor,
                    Cookie => CustomTextureImporterType.Cookie,
                    Lightmap => CustomTextureImporterType.Lightmap,
                    SingleChannel => CustomTextureImporterType.SingleChannel,
                    Shadowmask => CustomTextureImporterType.Shadowmask,
                    DirectionalLightmap => CustomTextureImporterType.DirectionalLightmap,
                    Default, Mask, Terrain, _ => CustomTextureImporterType.Default,
            };
        }
    }

    /// <summary>
    /// Utility class for texture type conversions
    /// </summary>
    public static class CustomTextureImporterTypeHelper
    {
        /// <summary>
        /// Convert string to CustomTextureImporterType enum
        /// </summary>
        public static CustomTextureImporterType? FromString(string typeName)
        {
            return typeName.ToLower() switch
            {
                "default" => CustomTextureImporterType.Default,
                "normalmap" or "normal" => CustomTextureImporterType.NormalMap,
                "gui" => CustomTextureImporterType.Gui,
                "sprite" => CustomTextureImporterType.Sprite,
                "cursor" => CustomTextureImporterType.Cursor,
                "cookie" => CustomTextureImporterType.Cookie,
                "lightmap" => CustomTextureImporterType.Lightmap,
                "singlechannel" => CustomTextureImporterType.SingleChannel,
                "shadowmask" => CustomTextureImporterType.Shadowmask,
                "directionallightmap" => CustomTextureImporterType.DirectionalLightmap,
                "mask" => CustomTextureImporterType.Mask,
                "terrain" => CustomTextureImporterType.Terrain,
                _ => null
            };
        }
    }

    // Texture type configuration for importer settings
    [Serializable]
    public class TextureTypeConfig
    {
        public List<string> patterns;

        public TextureTypeConfig()
        {
            patterns = new List<string>();
        }
    }

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
    
    public static void EnsurePSDSettingsForFile(string assetPath)
    {
        // Basic PSD handling - this is a simplified version
        // The actual implementation would depend on your specific PSD requirements
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    ///     Labels all assets with a single common asset bundle name, applying include/exclude patterns.
    /// </summary>
    /// <param name="bundleName">The asset bundle name (unique)</param>
    /// <param name="bundlePath">The name following Assets/Data in the assetbundle</param>
    /// <param name="includePatterns">Patterns to include (if empty, includes all)</param>
    /// <param name="excludePatterns">Patterns to exclude</param>
    /// <param name="textureTypes">Dictionary of texture type configurations</param>
    /// <returns>The asset bundle build info.</returns>
    public static AssetBundleBuild LabelAllAssetsWithCommonName(string bundleName, string bundlePath, 
        List<string> includePatterns = null, List<string> excludePatterns = null,
        Dictionary<string, TextureTypeConfig> textureTypes = null)
    {
        var sourceDirectory = Path.Combine(assetsFolder, bundlePath);
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
            
            // Apply includes first (if specified), then excludes
            if (!IsIncluded(relativePath, includePatterns)) {
                //Debug.Log($"Not included file: {relativePath}");
                continue;
            }
            
            if (IsExcluded(relativePath, excludePatterns)) {
                //Debug.Log($"Excluding file: {relativePath}");
                continue;
            }
            
            var assetPath = Path.Combine("Assets", "Data", bundlePath, relativePath).Replace('\\', '/');
           
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
            if (isPSD) EnsurePSDSettingsForFile(assetPath);

            // Set a common asset bundle name for every texture.
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                Debug.LogError($"[Warning] Could not get importer for: {assetPath}");
                continue;
            }
            importer.assetBundleName = bundleName;

            if (isTexture && importer is TextureImporter textureImporter)
            {
                bool isTerrain = assetPath.ToLower().Contains("/terrain/");

                // Determine texture type based on configured patterns
                var determinedType = DetermineTextureType(relativePath, textureTypes);
                
                // Apply texture type (with fallback logic)
                if (determinedType != null)
                {
                    // Use the ToTextureImporterType method to convert to Unity's type
                    textureImporter.textureType = (UnityEditor.TextureImporterType)determinedType.Value.ToTextureImporterType();
                }
                else
                {
                    textureImporter.textureType = UnityEditor.TextureImporterType.Default;
                }

                textureImporter.alphaIsTransparency = true;
                textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;

                // Check if the path has terrain in it, if so, set the wrap mode to Repeat.
                textureImporter.wrapMode = isTerrain ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                textureImporter.anisoLevel = isTerrain ? 8 : 1;

                textureImporter.filterMode = FilterMode.Trilinear;
                
                if ()
                textureImporter.mipmapEnabled = true;
                textureImporter.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                // Set sRGB based on texture type - masks and normal maps should not use sRGB
                bool isMaskType = determinedType == CustomTextureImporterType.Mask;
                textureImporter.sRGBTexture = !isMaskType && !assetPath.ToLower().Contains("_mask") && !assetPath.ToLower().Contains("_normal");

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
        }

        Debug.Log($"Labeling complete: {assetsLabeled} assets labeled with \"{bundleName}\".");
        return new AssetBundleBuild { assetBundleName = bundleName, assetNames = files.ToArray() };
    }

    private static bool IsExcluded(string relativePath, List<string> excludePatterns) {
        if (excludePatterns == null || excludePatterns.Count == 0)
            return false;

        // Normalize path separators for consistent matching
        var normalizedPath = relativePath.Replace('\\', '/');

        foreach (var pattern in excludePatterns) {
            // Convert glob pattern to regex
            var regexPattern = GlobToRegex(pattern);
            if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase)) return true;
        }

        return false;
    }

    private static bool IsIncluded(string relativePath, List<string> includePatterns) {
        if (includePatterns == null || includePatterns.Count == 0)
            return true;

        // Normalize path separators for consistent matching
        var normalizedPath = relativePath.Replace('\\', '/');

        foreach (var pattern in includePatterns) {
            // Convert glob pattern to regex
            var regexPattern = GlobToRegex(pattern);
            if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase)) return true;
        }

        return false;
    }

    private static CustomTextureImporterType? DetermineTextureType(string relativePath, Dictionary<string, TextureTypeConfig> textureTypes) {
        if (textureTypes == null || textureTypes.Count == 0)
            return null;

        // Normalize path separators for consistent matching
        var normalizedPath = relativePath.Replace('\\', '/');

        // Check each texture type configuration
        foreach (var kvp in textureTypes) {
            var typeName = kvp.Key.ToLower();
            var config = kvp.Value;

            if (config.patterns == null || config.patterns.Count == 0)
                continue;

            // Check if the file matches any pattern for this type
            foreach (var pattern in config.patterns) {
                var regexPattern = GlobToRegex(pattern);
                if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase)) {
                    // Use the helper method to convert string to enum
                    return CustomTextureImporterTypeHelper.FromString(typeName);
                }
            }
        }

        return null;
    }

    private static string GlobToRegex(string glob) {
        // Normalize path separators
        glob = glob.Replace('\\', '/');

        // Handle special case of **/ - matches any number of directories (including zero)
        if (glob.StartsWith("**/")) {
            var remainder = glob.Substring(3);
            var escapedRemainder = Regex.Escape(remainder)
                .Replace("\\*", "[^/]*") // * matches any character except /
                .Replace("\\?", "."); // ? matches single character

            // **/ allows the file to be at root or any depth
            return "^(.*/)?" + escapedRemainder + "$";
        }

        // Escape regex special characters except * and ?
        var escaped = Regex.Escape(glob);

        // Handle ** before * to avoid incorrect replacements
        escaped = escaped.Replace("\\*\\*", ".*"); // ** matches anything
        escaped = escaped.Replace("\\*", "[^/]*"); // * matches any character except /
        escaped = escaped.Replace("\\?", "."); // ? matches single character

        // If pattern doesn't start with /, it can match at any depth
        if (!glob.StartsWith("/"))
            escaped = "(^|.*/)" + escaped;

        // If pattern ends with /, match directories
        if (glob.EndsWith("/"))
            escaped = escaped + ".*";

        return "^" + escaped + "$";
    }
}