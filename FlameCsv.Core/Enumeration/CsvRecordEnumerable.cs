using System.Buffers;
using System.Collections;
using FlameCsv.Extensions;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerates known data into CSV records.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvRecordEnumerable<T> : IEnumerable<CsvValueRecord<T>> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySequence<T> _csv;
    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates a new instance that can be used to synchrounously enumerate records from the CSV.
    /// </summary>
    public CsvRecordEnumerable(ReadOnlyMemory<T> csv, CsvOptions<T> options)
        : this(new ReadOnlySequence<T>(csv), options)
    {
    }

    /// <summary>
    /// Creates a new instance that can be used to synchrounously enumerate records from the CSV.
    /// </summary>
    public CsvRecordEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _csv = csv;
        _options = options;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvRecordEnumerator<T> GetEnumerator()
    {
        Throw.IfDefaultStruct(_options is null, typeof(CsvRecordEnumerable<T>));
        return new(in _csv, _options);
    }

    /// <inheritdoc cref="CsvRecordAsyncEnumerable{T}.Preserve"/>
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
