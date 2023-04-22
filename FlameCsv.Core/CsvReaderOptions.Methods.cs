using CommunityToolkit.Diagnostics;

namespace FlameCsv;

public partial class CsvReaderOptions<T>
{
    /// <summary>
    /// Returns true if the row is empty or entirely whitespace (as defined in <paramref name="dialect"/>).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter.", Justification = "<Pending>")]
    public static bool DefaultRowSkipPredicate(ReadOnlySpan<T> row, in CsvDialect<T> dialect)
    {
        return row.IsEmpty;
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

        bool WhitespaceOrStartsWith(ReadOnlyMemory<T> data, in CsvDialect<T> _)
        {
            return data.IsEmpty || data.Span.StartsWith(tokenArray);
        }

        bool StartsWith(ReadOnlyMemory<T> data, in CsvDialect<T> _)
        {
            return data.Span.StartsWith(tokenArray);
        }
    }
}
