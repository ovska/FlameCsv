﻿using System.Buffers;
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
/// This type does not support <see cref="Parallel"/> or most LINQ operators.
/// </remarks>
[PublicAPI]
public sealed class CsvRecordEnumerable<T>
    : IEnumerable<CsvValueRecord<T>>, IAsyncEnumerable<CsvValueRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ICsvBufferReader<T> _reader;
    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(ReadOnlyMemory<T> csv, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _reader = CsvBufferReader.Create(csv);
        _options = options;
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _reader = CsvBufferReader.Create(in csv);
        _options = options;
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(ICsvBufferReader<T> reader, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        _reader = reader;
        _options = options;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvRecordEnumerator<T> GetEnumerator()
    {
        return new CsvRecordEnumerator<T>(_options, _reader);
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvRecordEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Copies the data in the records so they can be safely accessed after enumeration.
    /// </summary>
    public IEnumerable<CsvRecord<T>> Preserve()
    {
        foreach (ref readonly var csvRecord in this)
        {
            yield return new CsvRecord<T>(in csvRecord);
        }
    }

    /// <inheritdoc cref="Preserve"/>
    public async IAsyncEnumerable<CsvRecord<T>> PreserveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enumerator = GetAsyncEnumerator(cancellationToken);

        await using (enumerator.ConfigureAwait(false))
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                yield return new CsvRecord<T>(in enumerator.Current);
            }
        }
    }

    [MustDisposeResource]
    IEnumerator<CsvValueRecord<T>> IEnumerable<CsvValueRecord<T>>.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IAsyncEnumerator<CsvValueRecord<T>> IAsyncEnumerable<CsvValueRecord<T>>.GetAsyncEnumerator(
        CancellationToken cancellationToken)
        => GetAsyncEnumerator(cancellationToken);
}
