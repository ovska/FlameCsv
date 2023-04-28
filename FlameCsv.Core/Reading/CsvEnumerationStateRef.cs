using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;

namespace FlameCsv.Reading;

internal struct CsvEnumerationStateRef<T> where T : unmanaged, IEquatable<T>
{
    public readonly CsvReadingContext<T> _context;
    private readonly ReadOnlyMemory<T> _record;

    public ReadOnlyMemory<T> remaining;
    public Memory<T> buffer;
    public bool isAtStart;
    public uint quotesRemaining;
    public uint escapesRemaining;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvEnumerationStateRef(
        in CsvReadingContext<T> context,
        ReadOnlyMemory<T> record,
        ref T[]? array,
        RecordMeta? meta = null) : this(
            context: in context,
            record: record,
            remaining: record,
            isAtStart: true,
            meta: meta ?? context.GetRecordMeta(record),
            array: ref array)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvEnumerationStateRef(
        in CsvReadingContext<T> context,
        ReadOnlyMemory<T> record,
        ReadOnlyMemory<T> remaining,
        bool isAtStart,
        RecordMeta meta,
        ref T[]? array)
    {
        context.Dialect.DebugValidate();
        Debug.Assert(meta.quoteCount % 2 == 0);
        Debug.Assert(!isAtStart || record.Span.SequenceEqual(remaining.Span));
        Debug.Assert(remaining.IsEmpty || record.Span.Overlaps(remaining.Span));
        Debug.Assert(!Unsafe.IsNullRef(ref array));
        Debug.Assert(context.ArrayPool is not null);

        _context = context;
        _record = record;

        quotesRemaining = meta.quoteCount;
        escapesRemaining = meta.escapeCount;
        this.remaining = remaining;
        this.isAtStart = isAtStart;

        if (meta.HasSpecialCharacters)
        {
            context.ArrayPool.EnsureCapacity(ref array, meta.GetMaxUnescapedLength(remaining.Length));
            buffer = array;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void EnsureFullyConsumed(int fieldCount)
    {
        if (((uint)remaining.Length | quotesRemaining | escapesRemaining) == 0)
            return;

        ThrowNotFullyConsumed(fieldCount);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowRecordEndedPrematurely(int fieldCount, Type recordType)
    {
        throw new CsvFormatException(
            $"Expected the record to have {fieldCount} fields when parsing {recordType}, but it ended prematurely. " +
            $"Remaining: {_context.AsPrintableString(remaining)}, " +
            $"Record: {_context.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowFieldEndedPrematurely()
    {
        throw new UnreachableException(
            $"The record ended while having {quotesRemaining} quotes remaining. " +
            $"Record: {_context.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowParseFailed(ReadOnlySpan<T> field, ICsvParser<T> parser)
    {
        throw new CsvParseException(
            $"Failed to parse with {parser.GetType()} from {_context.AsPrintableString(field)} in {GetType().ToTypeString()}.")
        { Parser = parser };
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowEscapeAtEnd()
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (escape token was the final character), " +
            $"Remaining: {_context.AsPrintableString(remaining)}, " +
            $"Record: {_context.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowNoDelimiterAtHead()
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (no delimiter at head after the first field), " +
            $"Remaining: {_context.AsPrintableString(remaining)}, " +
            $"Record: {_context.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowNotFullyConsumed(int fieldCount)
    {
        StringBuilder sb = new(capacity: 128);

        if (quotesRemaining != 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"There were {quotesRemaining} leftover quotes in the state. ");
        }

        if (escapesRemaining != 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"There were {escapesRemaining} leftover escapes in the state. ");
        }

        if (!remaining.IsEmpty)
        {
            if (fieldCount != -1)
            {
                sb.Append(CultureInfo.InvariantCulture, $"Expected the record to have {fieldCount} fields, but it had more. ");
            }
            sb.Append(CultureInfo.InvariantCulture, $"Remaining: {_context.AsPrintableString(remaining)}, ");
        }

        sb.Append(CultureInfo.InvariantCulture, $"Record: {_context.AsPrintableString(_record)}");

        throw new CsvFormatException(sb.ToString());
    }

    /// <summary>
    /// Creates a new <see cref="CsvEnumerationStateRef{T}"/> from the given <paramref name="record"/>
    /// and returns a disposable that ensures the <paramref name="array"/> is returned to the context's pool.
    /// </summary>
    public static Lifetime CreateTemporary(
        in CsvReadingContext<T> context,
        ReadOnlyMemory<T> record,
        ref T[]? array,
        out CsvEnumerationStateRef<T> state)
    {
        state = new CsvEnumerationStateRef<T>(in context, record, ref array);
        return new Lifetime(context.ArrayPool, ref array);
    }

    public readonly ref struct Lifetime
    {
        private readonly ArrayPool<T> _arrayPool;
        private readonly Span<T[]?> _rentedBuffer;

        public void Dispose()
        {
            _arrayPool.EnsureReturned(ref _rentedBuffer[0]);
        }

        internal Lifetime(ArrayPool<T> arrayPool, ref T[]? array)
        {
            _arrayPool = arrayPool;
            _rentedBuffer = MemoryMarshal.CreateSpan(ref array, 1);
        }
    }
}
