using System.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv;

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
    private readonly CsvReaderOptions<T> _options;

    public CsvRecordEnumerable(ReadOnlyMemory<T> data, CsvReaderOptions<T> options)
        : this(new ReadOnlySequence<T>(data), options)
    {
    }


    public CsvRecordEnumerable(ReadOnlySequence<T> data, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();
        _data = data;
        _options = options;
    }

    public CsvRecordEnumerator<T> GetEnumerator()
    {
        GuardEx.EnsureNotDefaultStruct(_options);
        return new(_data, _options);
    }

    public IEnumerable<CsvRecord<T>> AsEnumerable()
    {
        GuardEx.EnsureNotDefaultStruct(_options);
        return new CopyingRecordEnumerable<T>(this);
    }
}
