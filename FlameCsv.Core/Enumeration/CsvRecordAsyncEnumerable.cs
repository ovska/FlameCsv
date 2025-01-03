using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

/// <summary>
/// Asynchronously reads data from a source into CSV records, use <see cref="CsvReader"/> to create.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvRecordAsyncEnumerable<T> : IAsyncEnumerable<CsvValueRecord<T>> where T : unmanaged, IEquatable<T>
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

    public CsvRecordAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        Throw.IfDefaultStruct<CsvRecordAsyncEnumerable<T>>(_options);
        return new(_reader, _options, cancellationToken);
    }

    public async IAsyncEnumerable<CsvRecord<T>> Preserve(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Throw.IfDefaultStruct<CsvRecordAsyncEnumerable<T>>(_options);
        
        await foreach (var csvRecord in this.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return new CsvRecord<T>(in csvRecord);
        }
    }

    IAsyncEnumerator<CsvValueRecord<T>> IAsyncEnumerable<CsvValueRecord<T>>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
