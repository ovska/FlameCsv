using FlameCsv.Reading;

namespace FlameCsv.Utilities;

internal ref struct BufferFieldReader<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySpan<T> _rawValue;
    private readonly ReadOnlySpan<Range> _ranges;
    private int _index;

    public BufferFieldReader(
        CsvOptions<T> options,
        ReadOnlyMemory<T> block,
        ReadOnlySpan<Range> ranges)
    {
        Options = options;
        _rawValue = block.Span;
        _ranges = ranges;
        _index = 0;
    }

    public CsvOptions<T> Options { get; }
    public int FieldCount => _ranges.Length;

    public bool TryReadNext(out ReadOnlySpan<T> field)
    {
        if (_index < _ranges.Length)
        {
            field = _rawValue[_ranges[_index++]];
            return true;
        }

        field = default;
        return false;
    }

    public ReadOnlySpan<T> this[int index] => _rawValue[_ranges[index]];
}
