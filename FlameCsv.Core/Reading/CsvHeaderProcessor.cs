using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

internal struct CsvHeaderProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvReaderOptions<T> _options;
    private readonly CsvReadingContext<T> _context;

    private CsvProcessor<T, TValue>? _inner;

    public CsvHeaderProcessor(CsvReaderOptions<T> options)
    {
        _options = options;
        _context = new CsvReadingContext<T>(options);
        _inner = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        if (_inner.HasValue)
        {
            // obs: don't use .Value here
            return _inner.DangerousGetValueOrDefaultReference().TryRead(ref buffer, out value, isFinalBlock);
        }

        return ParseHeaderAndTryRead(ref buffer, out value, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining more common paths
    private bool ParseHeaderAndTryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        if (_context.TryGetLine(ref buffer, out var line, out _, isFinalBlock))
        {
            ReadHeader(in line);

            // read the header unless the CSV consisted of a single record without trailing newline
            if (!isFinalBlock)
            {
                // obs: don't use .Value here
                return _inner.DangerousGetValueOrDefaultReference().TryRead(ref buffer, out value, isFinalBlock);
            }
        }

        value = default!;
        return false;
    }

    [MemberNotNull(nameof(_inner))]
    private void ReadHeader(in ReadOnlySequence<T> line)
    {
        using var view = new SequenceView<T>(in line, _options.ArrayPool);

        CsvBindingCollection<TValue> bindings;
        T[]? buffer = null;

        using (CsvEnumerationStateRefLifetime<T>.Create(in _context, view.Memory, ref buffer, out var state))
        {
            List<string> values = new(16);

            while (_context.TryGetField(ref state, out ReadOnlyMemory<T> field))
            {
                values.Add(_options.GetAsString(field.Span));
            }

            bindings = _options.GetHeaderBinder().Bind<TValue>(values);
        }

        var materializer = _options.CreateMaterializerFrom(bindings);
        _inner = new CsvProcessor<T, TValue>(in _context, materializer);
    }

    public void Dispose()
    {
        if (_inner.HasValue)
        {
            _inner.DangerousGetValueOrDefaultReference().Dispose();
        }
    }
}
