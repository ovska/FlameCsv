using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

[DebuggerDisplay(@"\{ RecordMeta, QuoteCount: {quoteCount}, EscapeCount: {escapeCount} \}")]
internal struct RecordMeta
{
    /// <summary>
    /// Amount of quotes-tokens not preceded with an escape character found in the line.
    /// </summary>
    public uint quoteCount;

    /// <summary>
    /// Amount of <em>effective</em> characters found in the line, e.g. an escaped escape <c>"\\"</c> counts as one.
    /// </summary>
    public uint escapeCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetMaxUnescapedLength(int valueLength)
    {
        int value;

        if (escapeCount == 0)
        {
            value = valueLength - (int)(quoteCount / 2);
        }
        else
        {
            Debug.Assert(quoteCount % 2 == 0, $"Invalid quote count {quoteCount}");
            value = valueLength - (int)escapeCount - (int)quoteCount;
        }

        Debug.Assert(value >= 0, "Value must >= 0");
        return value;
    }

    public readonly bool HasSpecialCharacters
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (quoteCount | escapeCount) != 0;
    }
}
