namespace FlameCsv;

/// <summary>
/// Options instance defining the tokens used when parsing.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly partial record struct CsvParserOptions<T>
{
    /// <summary>
    /// Column delimiter token.
    /// Default is <c>,</c> (comma).
    /// </summary>
    public T Delimiter { get; init; }

    /// <summary>
    /// String delimiter, between which all other tokens can appear as-is.
    /// When used in a string, must be preceded with another string delimiter.
    /// Default is <c>"</c> (double quote).
    /// </summary>
    public T StringDelimiter { get; init; }

    /// <summary>
    /// Newline tokens separating CSV rows. Must not be empty.
    /// Default is <c>CRLF</c>.
    /// </summary>
    public ReadOnlyMemory<T> NewLine { get; init; }

    /// <summary>
    /// Whitespace tokens to trim when parsing columns. Set to empty to process all columns as-is.
    /// Default is empty.
    /// </summary>
    public ReadOnlyMemory<T> Whitespace { get; init; }
}
