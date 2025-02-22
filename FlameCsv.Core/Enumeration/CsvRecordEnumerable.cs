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
/// This enumerable does not support parallel enumeration.
/// </remarks>
[PublicAPI]
public sealed class CsvRecordEnumerable<T>
    : IEnumerable<CsvValueRecord<T>>, IAsyncEnumerable<CsvValueRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(ReadOnlyMemory<T> csv, CsvOptions<T> options)
        : this(new ReadOnlySequence<T>(csv), options)
    {
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _reader = new ConstantPipeReader<T>(in csv);
        _options = options;
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvRecordEnumerable(ICsvPipeReader<T> reader, CsvOptions<T> options)
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
        // the ref-property is masked by the wrapping type
#pragma warning disable CA2007
        await using var enumerator = GetAsyncEnumerator(cancellationToken);
#pragma warning restore CA2007

        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            yield return new CsvRecord<T>(in enumerator.Current);
        }
    }

    [MustDisposeResource]
    IEnumerator<CsvValueRecord<T>> IEnumerable<CsvValueRecord<T>>.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    [MustDisposeResource]
    IAsyncEnumerator<CsvValueRecord<T>> IAsyncEnumerable<CsvValueRecord<T>>.GetAsyncEnumerator(
        CancellationToken cancellationToken)
    {
        return new CsvRecordEnumerator<T>(_options, _reader, cancellationToken);
    }
}
