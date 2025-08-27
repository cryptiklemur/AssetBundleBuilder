namespace CryptikLemur.AssetBundleBuilder.Attributes;

/// <summary>
///     Simple attribute to mark CLI arguments
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CliAttribute : Attribute {
    public CliAttribute(string longName, string? shortName = null, string? description = null) {
        LongName = longName;
        ShortName = shortName;
        Description = description;
    }

    public CliAttribute(int position, string? description = null) {
        IsPositional = true;
        Position = position;
        Description = description;
    }

    public string? LongName { get; }
    public string? ShortName { get; }
    public string? Description { get; }
    public bool IsPositional { get; }
    public int Position { get; }
}