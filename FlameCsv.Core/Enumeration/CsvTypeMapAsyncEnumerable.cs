using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

public sealed class CsvTypeMapAsyncEnumerable<T, TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvReadingContext<T> _context;
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvTypeMap<T, TValue> _typeMap;

    internal CsvTypeMapAsyncEnumerable(
        ICsvPipeReader<T> reader,
        in CsvReadingContext<T> context,
        CsvTypeMap<T, TValue> typeMap)
    {
        _reader = reader;
        _context = context;
        _typeMap = typeMap;
    }

    public CsvValueAsyncEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueAsyncEnumerator<T, TValue>(
            in _context,
            _typeMap,
            _reader,
            cancellationToken);
    }

    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
