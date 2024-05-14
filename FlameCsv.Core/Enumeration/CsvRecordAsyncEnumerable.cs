using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

/// <summary>
/// Asynchronously reads data from a source into CSV records, use <see cref="CsvReader"/> to create.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvRecordAsyncEnumerable<T> where T : unmanaged, IEquatable<T>
{
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvOptions<T> _options;

    internal CsvRecordAsyncEnumerable(ICsvPipeReader<T> reader, CsvOptions<T> options)
    {
        _reader = reader;
        _options = options;
    }

    public CsvRecordAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new(_reader, _options, cancellationToken);
    }

    public IAsyncEnumerable<CsvRecord<T>> AsAsyncEnumerable()
    {
        return new CopyingRecordAsyncEnumerable<T>(in this);
    }
}
