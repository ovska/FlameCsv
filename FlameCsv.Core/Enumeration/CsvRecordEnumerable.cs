using System.Buffers;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerates known data into CSV records.
/// </summary>
/// <remarks>
/// Maybe be enumerated multiple times, multiple concurrent enumerations are allowed.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvRecordEnumerable<T> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvOptions<T> _options;

    public CsvRecordEnumerable(ReadOnlyMemory<T> data, CsvOptions<T> options)
        : this(new ReadOnlySequence<T>(data), options)
    {
    }

    public CsvRecordEnumerable(in ReadOnlySequence<T> data, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _data = data;
        _options = options;
    }

    public CsvRecordEnumerator<T> GetEnumerator()
    {
        return new(in _data, _options);
    }

    public IEnumerable<CsvRecord<T>> AsEnumerable()
    {
        return new CopyingRecordEnumerable<T>(in this);
    }
}
