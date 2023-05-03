using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Runtime;

namespace FlameCsv.Reading;

internal struct CsvHeaderProcessor<T, [DynamicallyAccessedMembers(Trimming.ReflectionBound)] TValue> : ICsvProcessor<T, TValue>
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

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining common path
    // we don't want to annotate ICsvProcessor.TryRead because a headerless processor does not use dynamic code
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Trimming.HeaderProcessorSuppressionMessage)]
    private bool ParseHeaderAndTryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        ReadHeader:
        if (_context.TryGetLine(ref buffer, out var line, out _, isFinalBlock))
        {
            using var view = new SequenceView<T>(in line, _context.ArrayPool);

            var currentLine = _line;
            var currentPosition = _position;

            _line++;
            _position += line.Length + (!isFinalBlock).ToByte() * _context.Dialect.Newline.Length;

            if (_context.SkipRecord(view.Memory, currentLine))
            {
                goto ReadHeader;
            }

            CsvBindingCollection<TValue> bindings;
            T[]? array = null;

            using (CsvEnumerationStateRef<T>.CreateTemporary(in _context, view.Memory, ref array, out var state))
            {
                List<string> values = new(16);

                while (_context.TryGetField(ref state, out ReadOnlyMemory<T> field))
                {
                    values.Add(_context.Options.GetAsString(field.Span));
                }

                bindings = _context.Options.GetHeaderBinder().Bind<TValue>(values);
            }

            var materializer = _context.Options.CreateMaterializerFrom(bindings);
            _inner = new CsvProcessor<T, TValue>(
                context: in _context,
                materializer: materializer,
                line: _line,
                position: _position);

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

    public void Dispose()
    {
        _inner.DangerousGetValueOrDefaultReference().Dispose();
    }
}
