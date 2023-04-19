using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

public ref struct CsvEnumerationStateRef<T> where T : unmanaged, IEquatable<T>
{
    public readonly CsvDialect<T> Dialect { get; }
    public readonly ReadOnlyMemory<T> Record { get; }

    public readonly bool ExposeContent { get; }

    public ReadOnlyMemory<T> remaining;
    public Memory<T> unescapeBuffer;
    public bool isAtStart;

    public readonly ref int QuotesRemaining => ref _quotesRemaining[0];

    private readonly Span<int> _quotesRemaining;

    public CsvEnumerationStateRef(CsvReaderOptions<T> options, ReadOnlyMemory<T> record)
    {
        ArgumentNullException.ThrowIfNull(options);

        Dialect = new CsvDialect<T>(options);
        Record = record;
        ExposeContent = options.AllowContentInExceptions;

        int quoteCount = record.Span.Count(Dialect.Quote);
        _quotesRemaining = MemoryMarshal.CreateSpan(ref quoteCount, 1);

        remaining = record;
        isAtStart = true;

        if (quoteCount != 0)
        {
            T[]? buffer = null;
            var arrayPool = options.ArrayPool ?? AllocatingArrayPool<T>.Instance;
            arrayPool.EnsureCapacity(ref buffer, remaining.Length - quoteCount / 2);
            unescapeBuffer = buffer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvEnumerationStateRef(
        in CsvDialect<T> dialect,
        ReadOnlyMemory<T> record,
        ReadOnlyMemory<T> remaining,
        bool isAtStart,
        ref int quoteCount,
        ref T[]? buffer,
        ArrayPool<T> arrayPool,
        bool exposeContent)
    {
        dialect.DebugValidate();
        Debug.Assert(quoteCount >= 0 && quoteCount % 2 == 0);
        Debug.Assert(!isAtStart || record.Span.SequenceEqual(remaining.Span));
        Debug.Assert(remaining.IsEmpty || record.Span.Overlaps(remaining.Span));
        Debug.Assert(!Unsafe.IsNullRef(ref buffer));
        Debug.Assert(arrayPool is not null);

        Dialect = dialect;
        Record = record;
        ExposeContent = exposeContent;

        _quotesRemaining = MemoryMarshal.CreateSpan(ref quoteCount, 1);
        this.remaining = remaining;
        this.isAtStart = isAtStart;

        if (quoteCount != 0)
        {
            arrayPool.EnsureCapacity(ref buffer, remaining.Length - quoteCount / 2);
            unescapeBuffer = buffer;
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator
    {
        public ReadOnlyMemory<T> Current { readonly get; private set; }

        private CsvEnumerationStateRef<T> _state;

        public Enumerator(CsvEnumerationStateRef<T> state) => _state = state;

        public bool MoveNext()
        {
            if (RFC4180Mode<T>.TryGetField(ref _state, out ReadOnlyMemory<T> field))
            {
                Current = field;
                return true;
            }

            Current = default;
            return false;
        }
    }
}
