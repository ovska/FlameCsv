using System.Collections;
using FlameCsv.Reading;

namespace FlameCsv.Utilities;

#pragma warning disable IDE0064
internal ref struct BufferFieldReader<T> : ICsvFieldReader<T> where T : unmanaged, IEquatable<T>
#pragma warning restore IDE0064
{
    private readonly ReadOnlySpan<T> _block;
    private readonly ReadOnlySpan<Range> _ranges;
    private ReadOnlySpan<Range>.Enumerator _rangeEnumerator;

    // ReSharper disable once ConvertToPrimaryConstructor
    public BufferFieldReader(
        CsvOptions<T> options,
        ReadOnlySpan<T> record,
        ReadOnlySpan<T> block,
        ReadOnlySpan<Range> ranges)
    {
        _block = block;
        _ranges = ranges;
        _rangeEnumerator = ranges.GetEnumerator();
        Record = record;
        Options = options;
    }

    public ReadOnlySpan<T> Current { get; private set; }

    public ReadOnlySpan<T> Record { get; }
    public CsvOptions<T> Options { get; }

    public bool MoveNext()
    {
        if (_rangeEnumerator.MoveNext())
        {
            Current = _block[_rangeEnumerator.Current];
            return true;
        }

        return false;
    }

    public void Reset()
    {
        _rangeEnumerator = _ranges.GetEnumerator();
    }

    readonly object? IEnumerator.Current => throw new NotSupportedException();

    public void Dispose()
    {
        this = default;
    }
}
