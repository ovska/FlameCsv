using System.Runtime.CompilerServices;

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
        int end = buffer._starts[Start + Count - 1];
        int start = buffer._starts[Start];

        return end - start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRecord<T>(CsvReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        (int start, int length) = GetRecordBounds(reader._recordBuffer);
        ReadOnlySpan<T> data = reader._buffer.Span;
        return data.Slice(start, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int start, int length) GetRecordBounds(RecordBuffer buffer)
    {
        int start = buffer._starts[Start];
        int end = buffer._ends[Start + Count - 1];
        return (start, end - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int start, int length) GetFieldBounds(RecordBuffer buffer, int index)
    {
        int start = buffer._starts[Start + index];
        int end = buffer._ends[Start + index + 1];
        return (start, end - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetField<T>(CsvReader<T> reader, int index)
        where T : unmanaged, IBinaryInteger<T>
    {
        (int start, int length) = GetFieldBounds(reader._recordBuffer, index);
        ReadOnlySpan<T> data = reader._buffer.Span;
        return data.Slice(start, length);
    }

    public byte GetQuote(RecordBuffer buffer, int index)
    {
        return buffer._quotes[Start + index + 1];
    }
}
