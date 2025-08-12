using UnityEngine;
using UnityEditor;

public class PSDMatteUtility
{
    public static void EnsureSettingsForFile(string assetPath)
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
}