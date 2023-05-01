using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Runtime;

namespace FlameCsv.Reading;

internal struct CsvHeaderProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    public int Line => _line + _inner.DangerousGetValueOrDefaultReference().Line;
    public long Position => _position + _inner.DangerousGetValueOrDefaultReference().Position;

    private readonly CsvReadingContext<T> _context;

    private CsvProcessor<T, TValue>? _inner;

    private int _line;
    private long _position;

    public CsvHeaderProcessor(in CsvReadingContext<T> context)
    {
        _context = context;
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
        ReadHeader:
        if (_context.TryGetLine(ref buffer, out var line, out _, isFinalBlock))
        {
            using var view = new SequenceView<T>(in line, _context.ArrayPool);

            _line++;
            _position += line.Length + (!isFinalBlock).ToByte() * _context.Dialect.Newline.Length;

            if (_context.SkipRecord(view.Memory, _line))
            {
                goto ReadHeader;
            }

            ReadHeader(view.Memory);

            // read the first record now, unless the CSV consisted of just the header without trailing newline
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
    private void ReadHeader(ReadOnlyMemory<T> record)
    {
        CsvBindingCollection<TValue> bindings;
        T[]? buffer = null;

        using (CsvEnumerationStateRef<T>.CreateTemporary(in _context, record, ref buffer, out var state))
        {
            List<string> values = new(16);

            while (_context.TryGetField(ref state, out ReadOnlyMemory<T> field))
            {
                values.Add(_context.Options.GetAsString(field.Span));
            }

            bindings = _context.Options.GetHeaderBinder().Bind<TValue>(values);
        }

        var materializer = _context.Options.CreateMaterializerFrom(bindings);
        _inner = new CsvProcessor<T, TValue>(in _context, materializer);
    }

    public void Dispose()
    {
        _inner.DangerousGetValueOrDefaultReference().Dispose();
    }
}
