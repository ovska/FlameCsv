using System.Buffers;

namespace FlameCsv.Enumeration;

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
    private readonly CsvReadingContext<T> _context;

    public CsvRecordEnumerable(ReadOnlyMemory<T> data, CsvOptions<T> options, CsvContextOverride<T> overrides = default)
        : this(new ReadOnlySequence<T>(data), options, overrides)
    {
    }

    public CsvRecordEnumerable(in ReadOnlySequence<T> data, CsvOptions<T> options, CsvContextOverride<T> overrides = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        _data = data;
        _context = new CsvReadingContext<T>(options, in overrides);
    }

    public CsvRecordEnumerator<T> GetEnumerator()
    {
        _context.EnsureValid();
        return new(in _data, in _context);
    }

    public IEnumerable<CsvRecord<T>> AsEnumerable()
    {
        _context.EnsureValid();
        return new CopyingRecordEnumerable<T>(this);
    }
}
