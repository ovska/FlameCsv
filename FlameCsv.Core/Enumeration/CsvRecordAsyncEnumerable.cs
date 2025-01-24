using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Asynchronously reads data from a source into CSV records, use <see cref="CsvReader"/> to create.
/// </summary>
[PublicAPI]
public readonly struct CsvRecordAsyncEnumerable<T> : IAsyncEnumerable<CsvValueRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvOptions<T> _options;

    internal CsvRecordAsyncEnumerable(ICsvPipeReader<T> reader, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);

        _reader = reader;
        _options = options;
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvRecordAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        Throw.IfDefaultStruct(_options is null, typeof(CsvRecordAsyncEnumerable<T>));
        return new(_reader, _options, cancellationToken);
    }

    /// <summary>
    /// Copies the data in the records so they can be safely preserved even after enumeration.
    /// </summary>
    public async IAsyncEnumerable<CsvRecord<T>> Preserve(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Throw.IfDefaultStruct(_options is null, typeof(CsvRecordAsyncEnumerable<T>));

        await foreach (var csvRecord in this.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return new CsvRecord<T>(in csvRecord);
        }
    }

    [MustDisposeResource]
    IAsyncEnumerator<CsvValueRecord<T>> IAsyncEnumerable<CsvValueRecord<T>>.GetAsyncEnumerator(
        CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
