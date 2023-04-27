using FlameCsv.Reading;

namespace FlameCsv;

/// <summary>
/// Asynchronously reads data from a source into CSV records, use <see cref="CsvReader"/> to create.
/// </summary>
/// <remarks>
/// Maybe NOT be enumerated multiple times, multiple concurrent enumerations are NOT allowed.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvRecordAsyncEnumerable<T> where T : unmanaged, IEquatable<T>
{
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvReadingContext<T> _context;

    internal CsvRecordAsyncEnumerable(ICsvPipeReader<T> reader, in CsvReadingContext<T> context)
    {
        _reader = reader;
        _context = context;
    }

    public CsvRecordAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        _context.EnsureValid();
        return new(_reader, _context, cancellationToken);
    }

    public IAsyncEnumerable<CsvRecord<T>> AsAsyncEnumerable()
    {
        _context.EnsureValid();
        return new CopyingRecordAsyncEnumerable<T>(this);
    }
}
