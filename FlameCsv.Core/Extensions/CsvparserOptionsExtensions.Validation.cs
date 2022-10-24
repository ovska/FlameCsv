using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv.Extensions;

public static partial class CsvParserOptionsExtensions
{
    /// <summary>
    /// Returns possible validation errors in the options.
    /// </summary>
    /// <param name="options">Options to validate</param>
    /// <param name="errors">Non-null non-empty list of errors if options are invalid</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>True if the options instance is invalid</returns>
    public static bool TryGetValidationErrors<T>(
        in this CsvParserOptions<T> options,
        [NotNullWhen(true)] out List<string>? errors)
        where T : unmanaged, IEquatable<T>
    {
        List<string>? list = null;

        if (options.Equals(default))
        {
            AddError("The options instance is uninitialized.");
            errors = list!;
            return true;
        }

        if (options.Delimiter.Equals(options.StringDelimiter))
            AddError("Delimiter and StringDelimiter must not be equal.");

        if (options.NewLine.IsEmpty)
        {
            AddError("NewLine must not be empty.");
        }
        else
        {
            var newLine = options.NewLine.Span;

            if (newLine.Contains(options.Delimiter))
                AddError("NewLine must not contain Delimiter.");

            if (newLine.Contains(options.StringDelimiter))
                AddError("NewLine must not contain StringDelimiter.");
        }

        if (!options.Whitespace.IsEmpty)
        {
            var whitespace = options.Whitespace.Span;

            if (whitespace.Contains(options.Delimiter))
                AddError("Whitespace must not contain Delimiter.");

            if (whitespace.Contains(options.StringDelimiter))
                AddError("Whitespace must not contain StringDelimiter.");

            if (!options.NewLine.IsEmpty && whitespace.IndexOfAny(options.NewLine.Span) >= 0)
                AddError("NewLine and Whitespace must not have tokens in common.");
        }

        return (errors = list) is not null;

        void AddError(string message) => (list ??= new()).Add(message);
    }

    /// <summary>
    /// Throws if newline is empty, or there are duplicate symbols among any of the configuration tokens.
    /// </summary>
    /// <param name="options">Options to validate</param>
    /// <returns>The options instance</returns>
    /// <exception cref="CsvConfigurationException">Options are invalid</exception>
    public static CsvParserOptions<T> ThrowIfInvalid<T>(in this CsvParserOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        if (options.TryGetValidationErrors(out var errors))
        {
            throw new CsvConfigurationException(
                $"Invalid {typeof(CsvParserOptions<T>).ToTypeString()}: {string.Join(' ', errors)}");
        }

        return options;
    }
}
