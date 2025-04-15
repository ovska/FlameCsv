namespace FlameCsv.Writing;

/// <summary>
/// Enumeration that determines when CSV fields should be wrapped in quotes when writing.
/// </summary>
public enum CsvFieldQuoting : byte
{
    /// <summary>
    /// Fields are quoted only when they contain special characters (delimiters, quotes, newlines, escapes).
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Always quote fields, even if they don't contain any characters that need escaping.
    /// </summary>
    Always = 1,

    /// <summary>
    /// Same as <see cref="Auto"/>, but empty fields are quoted.
    /// </summary>
    Empty = 2,

    /// <summary>
    /// Same as <see cref="Empty"/>, but also quotes fields that contain leading or trailing spaces.
    /// </summary>
    LeadingOrTrailingSpaces = 3,

    /// <summary>
    /// Never quote or escape fields.
    /// </summary>
    /// <remarks>
    /// Can result in invalid CSV being written, use with caution.
    /// </remarks>
    Never = byte.MaxValue,
}
