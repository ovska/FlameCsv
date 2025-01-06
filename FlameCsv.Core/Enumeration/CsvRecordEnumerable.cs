using System.Buffers;
using System.Collections;
using FlameCsv.Extensions;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerates known data into CSV records.
/// </summary>
/// <remarks>
/// Maybe be enumerated multiple times, multiple concurrent enumerations are allowed.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvRecordEnumerable<T> : IEnumerable<CsvValueRecord<T>> where T : unmanaged, IEquatable<T>
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

    [MustDisposeResource]
    public CsvRecordEnumerator<T> GetEnumerator()
    {
        Throw.IfDefaultStruct(_options is null, typeof(CsvRecordEnumerable<T>));
        return new(in _data, _options);
    }

    public IEnumerable<CsvRecord<T>> Preserve()
    {
        Throw.IfDefaultStruct(_options is null, typeof(CsvRecordEnumerable<T>));

        foreach (var csvRecord in this)
        {
            yield return new CsvRecord<T>(in csvRecord);
        }
    }

    [MustDisposeResource]
    IEnumerator<CsvValueRecord<T>> IEnumerable<CsvValueRecord<T>>.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
