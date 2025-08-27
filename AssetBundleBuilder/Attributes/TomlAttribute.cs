namespace CryptikLemur.AssetBundleBuilder.Attributes;

/// <summary>
///     Attribute for TOML configuration mapping
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TomlAttribute : Attribute {
    public TomlAttribute(string name) {
        Name = name;
    }

    public string Name { get; }
}