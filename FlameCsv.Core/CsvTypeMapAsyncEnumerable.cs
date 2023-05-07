using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv;

internal sealed class CsvTypeMapAsyncEnumerable<T, TValue, TReader> : IAsyncEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
    where TReader : struct, ICsvPipeReader<T>
{
    private readonly CsvReadingContext<T> _context;
    private readonly TReader _reader;
    private readonly CsvTypeMap<T, TValue> _typeMap;

    public CsvTypeMapAsyncEnumerable(
        TReader reader,
        in CsvReadingContext<T> context,
        CsvTypeMap<T, TValue> typeMap)
    {
        _reader = reader;
        _context = context;
        _typeMap = typeMap;
    }

    public IAsyncEnumerator<TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_context.HasHeader)
        {
            return new CsvValueAsyncEnumerator<T, TValue, TReader, CsvHeaderProcessor<T, TValue>>(
                _reader,
                new CsvHeaderProcessor<T, TValue>(in _context, _typeMap),
                cancellationToken);
        }
        else
        {
            throw new NotSupportedException();
        }
    }
}
