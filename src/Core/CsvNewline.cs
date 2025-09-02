using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Enumeration that determines how newlines are handled in CSV files.
/// </summary>
[PublicAPI]
public enum CsvNewline : byte
{
    /// <summary>
    /// Read any of <c>\r\n</c>, <c>\n</c> and <c>\r</c>; write <c>\r\n</c>.
    /// </summary>
    /// <remarks>
    /// A lone <c>\n</c> or <c>\r</c> will be treated as a newline character when reading for compatibility.
    /// </remarks>
    CRLF = 0,

    /// <summary>
    /// Read and write only <c>\n</c>.
    /// </summary>
    /// <remarks>
    /// You can get a performance boost by using this when you know that your data only contains <c>\n</c> newlines.
    /// </remarks>
    LF = 1,

    /// <summary>
    /// Use either <see cref="CRLF"/> or <see cref="LF"/> depending on <see cref="Environment.NewLine"/>.
    /// </summary>
    /// <remarks>
    /// Platform detected <c>\r\n</c> has the same semantics as <see cref="CRLF"/>.
    /// </remarks>
    Platform = 2,
}
