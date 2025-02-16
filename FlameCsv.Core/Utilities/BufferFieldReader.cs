using FlameCsv.Reading;

namespace FlameCsv.Utilities;

internal readonly ref struct BufferFieldReader<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySpan<T> _rawValue;
    private readonly ReadOnlySpan<Range> _ranges;

    public BufferFieldReader(ReadOnlySpan<T> block, ReadOnlySpan<Range> ranges)
    {
        _rawValue = block;
        _ranges = ranges;
    }

    public int FieldCount => _ranges.Length;
    public ReadOnlySpan<T> this[int index] => _rawValue[_ranges[index]];
}
