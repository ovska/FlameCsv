using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv;

public sealed class CsvValueEnumerable<T, [DynamicallyAccessedMembers(Trimming.ReflectionBound)] TValue> : IEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvReadingContext<T> _context;
    private readonly CsvTypeMap<TValue>? _typeMap;

    [RequiresUnreferencedCode(Trimming.CompiledExpressions)]
    public CsvValueEnumerable(
        in ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> overrides)
    {
        ArgumentNullException.ThrowIfNull(options);
        _data = csv;
        _context = new CsvReadingContext<T>(options, overrides);
    }

    public CsvValueEnumerable(
        in ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> overrides,
        CsvTypeMap<TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);

        _data = csv;
        _context = new CsvReadingContext<T>(options, overrides);
        _typeMap = typeMap;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Runtime guarded.")]
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
            var materializer = _typeMap is null
                ? _context.Options.GetMaterializer<T, TValue>()
                : _typeMap.GetMaterializer(in _context);

            return new CsvValueEnumerator<T, TValue, CsvProcessor<T, TValue>>(
                _data,
                new CsvProcessor<T, TValue>(in _context, materializer));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
