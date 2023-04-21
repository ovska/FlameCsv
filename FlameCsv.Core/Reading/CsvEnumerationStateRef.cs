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

internal struct CsvEnumerationStateRef<T> where T : unmanaged, IEquatable<T>
{
    public readonly CsvDialect<T> Dialect { get; }
    public readonly ReadOnlyMemory<T> Record { get; }

    public readonly bool ExposeContent { get; }

    public ReadOnlyMemory<T> remaining;
    public Memory<T> buffer;
    public bool isAtStart;
    public uint quotesRemaining;
    public uint escapesRemaining;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvEnumerationStateRef(
        in CsvDialect<T> dialect,
        ReadOnlyMemory<T> record,
        ReadOnlyMemory<T> remaining,
        bool isAtStart,
        RecordMeta meta,
        ref T[]? array,
        ArrayPool<T> arrayPool,
        bool exposeContent)
    {
        dialect.DebugValidate();
        Debug.Assert(meta.quoteCount % 2 == 0);
        Debug.Assert(!isAtStart || record.Span.SequenceEqual(remaining.Span));
        Debug.Assert(remaining.IsEmpty || record.Span.Overlaps(remaining.Span));
        Debug.Assert(!Unsafe.IsNullRef(ref array));
        Debug.Assert(arrayPool is not null);

        Dialect = dialect;
        Record = record;
        ExposeContent = exposeContent;

        quotesRemaining = meta.quoteCount;
        escapesRemaining = meta.escapeCount;
        this.remaining = remaining;
        this.isAtStart = isAtStart;

        if (meta.HasSpecialCharacters)
        {
            arrayPool.EnsureCapacity(ref array, meta.GetMaxUnescapedLength(remaining.Length));
            buffer = array;
        }
    }

    public CsvEnumerationStateRef(CsvReaderOptions<T> options, ReadOnlyMemory<T> record)
    {
        ArgumentNullException.ThrowIfNull(options);

        CsvDialect<T> dialect = new(options);

        Dialect = dialect;
        Record = record;
        ExposeContent = options.AllowContentInExceptions;

        var meta = dialect.GetRecordMeta(record, options.AllowContentInExceptions);
        quotesRemaining = meta.quoteCount;
        escapesRemaining = meta.escapeCount;
        remaining = record;
        isAtStart = true;

        if (meta.HasSpecialCharacters)
        {
            T[]? array = null;
            var arrayPool = options.ArrayPool.AllocatingIfNull();
            arrayPool.EnsureCapacity(ref array, meta.GetMaxUnescapedLength(record.Length));
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
            sb.Append(CultureInfo.InvariantCulture, $"Remaining: {remaining.Span.AsPrintableString(ExposeContent, Dialect)}, ");
        }

        sb.Append(CultureInfo.InvariantCulture, $"Record: {Record.Span.AsPrintableString(ExposeContent, Dialect)}");

        throw new CsvFormatException(sb.ToString());
    }
}
