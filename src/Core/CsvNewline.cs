using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Enumeration that determines how newlines are handled in CSV files.
/// </summary>
[PublicAPI]
public enum CsvNewline : byte
{
    /// <summary>
    /// Read and write <c>\r\n</c> as the newline character.
    /// </summary>
    /// <remarks>
    /// A lone <c>\n</c> or <c>\r</c> will be treated as a newline character when reading for compatibility.
    /// </remarks>
    CRLF = 0,

    /// <summary>
    /// Read and write only <c>\n</c> as the newline character.
    /// </summary>
    LF = 1,

    /// <summary>
    /// Use either <see cref="CRLF"/> or <see cref="LF"/> depending on <see cref="Environment.NewLine"/>.<br/>
    /// This will be <c>\r\n</c> on Windows and <c>\n</c> on Unix-like systems.
    /// </summary>
    /// <remarks>
    /// Platform detected <c>\r\n</c> has the same semantics as <see cref="CRLF"/>.
    /// </remarks>
    Platform = 2,
}
