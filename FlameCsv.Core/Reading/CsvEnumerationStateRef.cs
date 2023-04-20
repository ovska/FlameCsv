using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

public struct CsvEnumerationStateRef<T> where T : unmanaged, IEquatable<T>
{
    public readonly CsvDialect<T> Dialect { get; }
    public readonly ReadOnlyMemory<T> Record { get; }

    public readonly bool ExposeContent { get; }

    public ReadOnlyMemory<T> remaining;
    public Memory<T> buffer;
    public bool isAtStart;
    public int quotesRemaining;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvEnumerationStateRef(
        in CsvDialect<T> dialect,
        ReadOnlyMemory<T> record,
        ReadOnlyMemory<T> remaining,
        bool isAtStart,
        int quoteCount,
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

        quotesRemaining = quoteCount;
        this.remaining = remaining;
        this.isAtStart = isAtStart;

        if (quoteCount != 0)
        {
            arrayPool.EnsureCapacity(ref buffer, remaining.Length - quoteCount / 2);
            this.buffer = buffer;
        }
    }

    public CsvEnumerationStateRef(CsvReaderOptions<T> options, ReadOnlyMemory<T> record)
    {
        ArgumentNullException.ThrowIfNull(options);

        Dialect = new CsvDialect<T>(options);
        Record = record;
        ExposeContent = options.AllowContentInExceptions;

        int quoteCount = record.Span.Count(Dialect.Quote);
        quotesRemaining = quoteCount;
        remaining = record;
        isAtStart = true;

        if (quoteCount != 0)
        {
            T[]? buffer = null;
            var arrayPool = options.ArrayPool.AllocatingIfNull();
            arrayPool.EnsureCapacity(ref buffer, remaining.Length - quoteCount / 2);
            this.buffer = buffer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void EnsureFullyConsumed(int fieldCount)
    {
        if (remaining.IsEmpty && quotesRemaining == 0)
            return;

        ThrowNotFullyConsumed(fieldCount);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowNotFullyConsumed(int fieldCount)
    {
        StringBuilder sb = new(capacity: 128);

        if (quotesRemaining != 0)
        {
            sb.Append($"There were {quotesRemaining} leftover quotes in the state. ");
        }

        if (!remaining.IsEmpty)
        {
            sb.Append($"Expected the record to have {fieldCount} fields, but it had more. ");
            sb.Append($"Remaining: {remaining.Span.AsPrintableString(ExposeContent, Dialect)}, ");
        }

        sb.Append($"Record: {Record.Span.AsPrintableString(ExposeContent, Dialect)}");

        throw new CsvFormatException(sb.ToString());
    }

    public readonly Enumerator GetEnumerator() => new(this);

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
