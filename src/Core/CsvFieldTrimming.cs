using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Enumeration that determines how leading and trailing spaces is handled when reading CSV fields.
/// </summary>
[PublicAPI]
[Flags]
public enum CsvFieldTrimming : byte
{
    /// <summary>
    /// No trimming is performed. Fields are read as-is, including leading and trailing spaces.
    /// </summary>
    None = 0,

    /// <summary>
    /// Leading ASCII spaces are trimmed from the start of the field and before quotes.
    /// </summary>
    Leading = 1,

    /// <summary>
    /// Trailing ASCII spaces are trimmed from the end of the field and after quotes.
    /// </summary>
    Trailing = 2,

    /// <summary>
    /// Both leading and trailing ASCII spaces are trimmed from the field and around quotes.
    /// </summary>
    Both = Leading | Trailing,
}
