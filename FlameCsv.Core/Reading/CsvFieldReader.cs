using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

internal struct CsvFieldReader<T> : ICsvFieldReader<T> where T : unmanaged, IEquatable<T>
{
    public readonly CsvReadingContext<T> _context;
    private readonly ReadOnlyMemory<T> _record;

    public ReadOnlyMemory<T> remaining;
    public Memory<T> buffer;
    public bool isAtStart;
    public uint quotesRemaining;
    public uint escapesRemaining;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldReader(
        in CsvReadingContext<T> context,
        ReadOnlyMemory<T> record,
        ref T[]? array) : this(in context, record, ref array, context.GetRecordMeta(record))
    {
    }

    internal CsvFieldReader(
        in CsvReadingContext<T> context,
        ReadOnlyMemory<T> record,
        ref T[]? array,
        RecordMeta meta)
    {
        context.Dialect.DebugValidate();
        Debug.Assert(meta.quoteCount % 2 == 0);
        Debug.Assert(remaining.IsEmpty || record.Span.Overlaps(remaining.Span));
        Debug.Assert(!Unsafe.IsNullRef(ref array));
        Debug.Assert(context.ArrayPool is not null);

        _context = context;
        _record = record;
        remaining = record;

        isAtStart = true;
        quotesRemaining = meta.quoteCount;
        escapesRemaining = meta.escapeCount;

        if (meta.HasSpecialCharacters)
        {
            context.ArrayPool.EnsureCapacity(ref array, meta.GetMaxUnescapedLength(remaining.Length));
            buffer = array;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadNext(out ReadOnlyMemory<T> field)
    {
        if (!remaining.IsEmpty)
        {
            field = _context.Dialect.IsRFC4180Mode
                ? RFC4180Mode<T>.ReadNextField(ref this)
                : EscapeMode<T>.ReadNextField(ref this);
            return true;
        }

        Unsafe.SkipInit(out field);
        return false;
    }

    readonly void ICsvFieldReader<T>.TryEnsureFieldCount(int fieldCount)
    {
    }

    public readonly void EnsureFullyConsumed(int fieldCount)
    {
        if (((uint)remaining.Length | quotesRemaining | escapesRemaining) == 0)
            return;

        ThrowNotFullyConsumed(fieldCount);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowParseFailed(ReadOnlyMemory<T> field, CsvConverter<T>? parser)
    {
        string withStr = parser is null ? "" : $" with {parser.GetType()}";

        throw new CsvParseException(
            $"Failed to parse{withStr} from {_context.AsPrintableString(field)}.")
        { Parser = parser };
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowForInvalidEOF()
    {
        string escapeStr = _context.Dialect.IsRFC4180Mode ? "" : $"and {escapesRemaining} escapes ";
        throw new UnreachableException(
            $"The record ended while having {quotesRemaining} quotes {escapeStr}remaining. " +
            $"Record: {_context.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowForInvalidEndOfString()
    {
        throw new UnreachableException(
            "The record had a string that ended in the middle without the next character being a delimiter. " +
            $"Record: {_context.AsPrintableString(_record)}");
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
    public readonly void ThrowNotFullyConsumed(int fieldCount)
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
    /// Creates a new <see cref="CsvFieldReader{T}"/> from the given <paramref name="record"/>
    /// and returns a disposable that ensures the <paramref name="array"/> is returned to the context's pool.
    /// </summary>
    internal static Lifetime CreateTemporary(
        in CsvReadingContext<T> context,
        ReadOnlyMemory<T> record,
        ref T[]? array,
        out CsvFieldReader<T> state)
    {
        state = new CsvFieldReader<T>(in context, record, ref array);
        return new Lifetime(context.ArrayPool, ref array);
    }

    internal readonly ref struct Lifetime
    {
        private readonly ArrayPool<T> _arrayPool;
        private readonly ref T[]? _rentedBuffer;

        public void Dispose()
        {
            _arrayPool.EnsureReturned(ref _rentedBuffer);
        }

        internal Lifetime(ArrayPool<T> arrayPool, ref T[]? array)
        {
            _arrayPool = arrayPool;
            _rentedBuffer = ref array;
        }
    }
}
