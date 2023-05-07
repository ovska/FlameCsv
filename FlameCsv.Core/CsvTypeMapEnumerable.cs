using System.Buffers;
using System.Collections;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv;

public sealed class CsvTypeMapEnumerable<T, TValue> : IEnumerable<TValue> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvReadingContext<T> _context;
    private readonly CsvTypeMap<T, TValue> _typeMap;

    public CsvTypeMapEnumerable(
        in ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> overrides,
        CsvTypeMap<T, TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);

        _data = csv;
        _context = new CsvReadingContext<T>(options, overrides);
        _typeMap = typeMap;
    }

    public IEnumerator<TValue> GetEnumerator()
    {
        if (_context.HasHeader)
        {
            return new CsvValueEnumerator<T, TValue, CsvHeaderProcessor<T, TValue>>(
                _data,
                new CsvHeaderProcessor<T, TValue>(in _context, _typeMap));
        }
        else
        {
            throw new NotSupportedException("TODO");
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
