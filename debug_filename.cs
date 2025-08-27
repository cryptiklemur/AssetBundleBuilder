using CryptikLemur.AssetBundleBuilder;
using CryptikLemur.AssetBundleBuilder.Utilities;

var args = new[] { 
    "2022.3.35f1", "Assets", "testbundle", "Output",
    "--filename", "custom_{bundle}_{platform}"
};

Console.WriteLine("Testing filename argument parsing...");

try {
    var config = ArgumentParser.Parse(args);
    Console.WriteLine($"Config is null: {config == null}");
    if (config != null) {
        Console.WriteLine($"Filename: '{config.Filename}'");
        Console.WriteLine($"Expected: 'custom_{{bundle}}_{{platform}}'");
        Console.WriteLine($"Match: {config.Filename == "custom_{bundle}_{platform}"}");
    }
} catch (Exception ex) {
    Console.WriteLine($"Exception: {ex.Message}");
    Console.WriteLine($"Type: {ex.GetType().Name}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}