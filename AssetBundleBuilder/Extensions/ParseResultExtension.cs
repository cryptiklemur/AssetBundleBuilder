using System.CommandLine.Parsing;

namespace CryptikLemur.AssetBundleBuilder.Extensions;

public static class ParseResultExtension {
    public static bool HasOption(this ParseResult parseResult, string alias) {
        return parseResult.Tokens.Any(t => t.Value == alias);
    }

    public static T? GetValue<T>(this ParseResult parseResult, string name) {
        // Try to get from options first
        foreach (var option in parseResult.CommandResult.Command.Options) {
            if (option.Aliases.Contains(name) || option.Name == name) {
                return parseResult.GetValueForOption(option) is T value ? value : default;
            }
        }

        // Then try arguments
        foreach (var argument in parseResult.CommandResult.Command.Arguments) {
            if (argument.Name == name) {
                return parseResult.GetValueForArgument(argument) is T value ? value : default;
            }
        }

        return default;
    }
}