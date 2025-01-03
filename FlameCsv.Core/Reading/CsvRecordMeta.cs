using System.Diagnostics;

namespace FlameCsv.Reading;

[DebuggerDisplay(@"\{ RecordMeta, QuoteCount: {quoteCount}, EscapeCount: {escapeCount} \}")]
public struct CsvRecordMeta
{
    /// <summary>
    /// Amount of quotes-tokens not preceded with an escape character found in the line.
    /// </summary>
    public uint quoteCount;

    /// <summary>
    /// Amount of <em>effective</em> escape-tokens found in the line, e.g. an escaped escape <c>"\\"</c> counts as one.
    /// </summary>
    public uint escapeCount;
}
