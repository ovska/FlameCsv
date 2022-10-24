using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv;

public sealed partial class CsvConfiguration<T>
{
    /// <summary>
    /// Returns true if the row is empty or entirely whitespace (as defined in <paramref name="options"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DefaultRowSkipPredicate(ReadOnlySpan<T> row, in CsvParserOptions<T> options)
    {
        return row.IsEmpty
            || (!options.Whitespace.IsEmpty && row.TrimStart(options.Whitespace.Span).IsEmpty);
    }

    /// <summary>
    /// Returns a delegate that checks if the input starts with <paramref name="tokens"/>.
    /// </summary>
    /// <param name="tokens">
    /// Tokens to check for. They are copied to a new array when the delegate is created.
    /// </param>
    /// <param name="skipEmptyOrWhitespace">
    /// Whether empty or entirely whitespace inputs return true, see <see cref="DefaultRowSkipPredicate"/>.
    /// </param>
    /// <exception cref="ArgumentException">Parameter span is empty</exception>
    public static CsvCallback<T, bool> SkipIfStartsWith(
        ReadOnlySpan<T> tokens,
        bool skipEmptyOrWhitespace = true)
    {
        Guard.IsNotEmpty(tokens);

        var tokenArray = tokens.ToArray();
        return skipEmptyOrWhitespace ? WhitespaceOrStartsWith : StartsWith;

        bool WhitespaceOrStartsWith(ReadOnlySpan<T> data, in CsvParserOptions<T> options)
        {
            return DefaultRowSkipPredicate(data, in options) || data.StartsWith(tokenArray.AsSpan());
        }

        bool StartsWith(ReadOnlySpan<T> data, in CsvParserOptions<T> _)
        {
            return data.StartsWith(tokenArray.AsSpan());
        }
    }
}
