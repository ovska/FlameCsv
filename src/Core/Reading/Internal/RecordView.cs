using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

internal readonly struct RecordView
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordView(int start, int count)
    {
        Start = start;
        Count = count;
    }

    public int Start { get; }
    public int Count { get; }

    public int FieldCount => Count - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetLengthWithNewline(RecordBuffer buffer)
    {
        // only the original field metadata knows if this is a CRLF or not
        int end = Field.NextStart(buffer._fields[Start + Count - 1]);
        int start = buffer._starts[Start];

        return end - start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int start, int length) GetRecord(RecordBuffer buffer)
    {
        int start = buffer._starts[Start];
        int end = buffer._ends[Start + Count - 1];
        return (start, end - start);
    }

    public (int start, int length) GetField(RecordBuffer buffer, int index)
    {
        int start = buffer._starts[Start + index];
        int end = buffer._ends[Start + index + 1];
        return (start, end - start);
    }

    public byte GetQuote(RecordBuffer buffer, int index)
    {
        return buffer._quotes[Start + index + 1];
    }
}
