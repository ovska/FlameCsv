namespace FlameCsv.Writing;

/// <summary>
/// Enumeration that determines field escaping and quoting behavior when writing CSV.
/// </summary>
public enum CsvFieldEscaping
{
    /// <summary>
    /// Only quote and escape fields when needed.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Always quote fields, even if they don't contain any characters that need escaping.
    /// </summary>
    AlwaysQuote = 1,

    /// <summary>
    /// Never quote fields. Can result in invalid CSV being written, as possible delimiters,
    /// quotes, or newlines will not be escaped. Should only be used if you are absolutely sure
    /// that the data does not contain any of these characters.
    /// </summary>
    Never = 2,
}
