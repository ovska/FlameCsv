namespace FlameCsv.Writing;

/// <summary>
/// Enumeration that determines when CSV fields should be wrapped in quotes when writing.
/// </summary>
public enum CsvFieldQuoting
{
    /// <summary>
    /// Fields are quoted only when they contain special characters (delimiters, quotes, newlines, escapes).
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Always quote fields, even if they don't contain any characters that need escaping.
    /// </summary>
    AlwaysQuote = 1,

    /// <summary>
    /// Never quote fields.
    /// </summary>
    /// <remarks>
    /// Can result in invalid CSV being written,use with caution.
    /// </remarks>
    Never = 2,
}
