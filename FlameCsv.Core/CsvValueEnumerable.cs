using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv;

[RequiresUnreferencedCode(Trimming.CompiledExpressions)]
public sealed class CsvValueEnumerable<T, [DynamicallyAccessedMembers(Trimming.ReflectionBound)] TValue> : IEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvReadingContext<T> _context;

    public CsvValueEnumerable(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> overrides)
        : this(new ReadOnlySequence<T>(csv), options, overrides)
    {
    }

    public CsvValueEnumerable(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> overrides)
    {
        ArgumentNullException.ThrowIfNull(options);
        _data = csv;
        _context = new CsvReadingContext<T>(options, overrides);
    }

    public IEnumerator<TValue> GetEnumerator()
    {
        if (_context.HasHeader)
        {
            return new CsvValueEnumerator<T, TValue, CsvHeaderProcessor<T, TValue>>(
                _data,
                new CsvHeaderProcessor<T, TValue>(in _context));
        }
        else
        {
            return new CsvValueEnumerator<T, TValue, CsvProcessor<T, TValue>>(
                _data,
                new CsvProcessor<T, TValue>(in _context, _context.Options.GetMaterializer<T, TValue>()));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
