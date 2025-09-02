using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Flags enumeration that determines when CSV fields should be wrapped in quotes when writing.
/// </summary>
/// <remarks>
/// Multiple flags can be combined to apply several quoting rules. To quote both empty fields and those
/// that contain control characters, use <c>CsvFieldQuoting.Empty | CsvFieldQuoting.Auto</c>.
/// </remarks>
[Flags]
[PublicAPI]
public enum CsvFieldQuoting
{
    /// <summary>
    /// Never quote or escape fields.
    /// </summary>
    /// <remarks>
    /// Can result in invalid CSV being written, use with caution.
    /// </remarks>
    Never = 0,

    /// <summary>
    /// Quote fields that contain control characters (delimiters, quotes, newlines, or escapes).
    /// This is the default behavior.
    /// </summary>
    Auto = 1 << 0,

    /// <summary>
    /// Quote empty fields.
    /// </summary>
    Empty = 1 << 1,

    /// <summary>
    /// Quote fields that contain leading spaces.
    /// </summary>
    LeadingSpaces = 1 << 2,

    /// <summary>
    /// Quote fields that contain trailing spaces.
    /// </summary>
    TrailingSpaces = 1 << 3,

    /// <summary>
    /// Quote fields that contain leading or trailing spaces.
    /// </summary>
    LeadingOrTrailingSpaces = LeadingSpaces | TrailingSpaces,

    /// <summary>
    /// Always quote all fields, even if they don't contain any characters that need escaping.
    /// </summary>
    Always = ~0,
}
