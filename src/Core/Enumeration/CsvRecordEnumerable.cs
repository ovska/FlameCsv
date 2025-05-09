using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.IO;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerates known data into CSV records.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <remarks>
/// As only one <see cref="CsvRecord{T}"/> can be used at a time,
/// this type does not support <see cref="Parallel"/> or most LINQ operators.
/// </remarks>
[PublicAPI]
public sealed class CsvRecordEnumerable<T> : IEnumerable<CsvRecord<T>>, IAsyncEnumerable<CsvRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReaderFactory<T> _reader;
    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(ReadOnlyMemory<T> csv, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _reader = new(CsvBufferReader.Create(csv));
        _options = options;
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _reader = new(CsvBufferReader.Create(in csv));
        _options = options;
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(ICsvBufferReader<T> reader, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        _reader = new(reader);
        _options = options;
    }

    internal CsvRecordEnumerable(ReaderFactory<T> factory, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _reader = factory;
        _options = options;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvRecordEnumerator<T> GetEnumerator()
    {
        return new CsvRecordEnumerator<T>(_options, _reader.Create(false));
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvRecordEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvRecordEnumerator<T>(_options, _reader.Create(true), cancellationToken);
    }

    /// <summary>
    /// Copies the data in the records so they can be safely accessed after enumeration.
    /// </summary>
    public IEnumerable<CsvPreservedRecord<T>> Preserve()
    {
        foreach (ref readonly var csvRecord in this)
        {
            yield return new CsvPreservedRecord<T>(in csvRecord);
        }
    }

    /// <inheritdoc cref="Preserve"/>
    public async IAsyncEnumerable<CsvPreservedRecord<T>> PreserveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var enumerator = GetAsyncEnumerator(cancellationToken);

        await using (enumerator.ConfigureAwait(false))
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                yield return new CsvPreservedRecord<T>(in enumerator.Current);
            }
        }
    }

    [MustDisposeResource]
    IEnumerator<CsvRecord<T>> IEnumerable<CsvRecord<T>>.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IAsyncEnumerator<CsvRecord<T>> IAsyncEnumerable<CsvRecord<T>>.GetAsyncEnumerator(
        CancellationToken cancellationToken
    ) => GetAsyncEnumerator(cancellationToken);
}
