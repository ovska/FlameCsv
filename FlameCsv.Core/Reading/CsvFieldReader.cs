using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

internal static class CsvEnumerationExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadNext<T>(ref this CsvFieldReader<T> record, out ReadOnlySpan<T> field)
        where T : unmanaged, IEquatable<T>
    {
        return CsvFieldReader<T>.TryReadNext(ref record, out field);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadNext<T>(ref this CsvFieldReader<T> record, out ReadOnlyMemory<T> field)
        where T : unmanaged, IEquatable<T>
    {
        if (CsvFieldReader<T>.TryReadNext(ref record, out var fieldSpan))
        {
            field = record.GetAsMemory(fieldSpan);
            return true;
        }

        field = default;
        return false;
    }
}

public ref struct CsvFieldReader<T> where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadNext(ref CsvFieldReader<T> reader, out ReadOnlySpan<T> field)
    {
        if (!reader.End)
        {
            field = !reader.Escape.HasValue
                ? RFC4180Mode<T>.ReadNextField(ref reader)
                : UnixMode<T>.ReadNextField(ref reader);
            return true;
        }

        field = default;
        return false;
    }

    public readonly ReadOnlySpan<T> Record { get; }
    public readonly ReadOnlySpan<T> Remaining => Record.Slice(Consumed);
    public readonly bool End => Consumed >= Record.Length;

    public int Consumed { get; internal set; }

    public bool isAtStart;
    public uint quotesRemaining;
    public uint escapesRemaining;

    public readonly T Delimiter => _options._delimiter;
    public readonly T Quote => _options._quote;
    public readonly T? Escape => _options._escape;
    public readonly ReadOnlySpan<T> Whitespace { get; }

    private readonly CsvOptions<T> _options;
    private readonly ReadOnlyMemory<T> _record;
    private readonly Span<T> _unescapeBuffer;
    private readonly ref T[]? _unescapeArray;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldReader(
        CsvOptions<T> options,
        ReadOnlyMemory<T> record,
        Span<T> unescapeBuffer,
        ref T[]? unescapeArray,
        ref readonly CsvRecordMeta meta)
    {
        Record = record.Span;

        _options = options;
        Whitespace = options._whitespace.Span;

        _record = record;

        _unescapeBuffer = unescapeBuffer;
        _unescapeArray = ref unescapeArray;

        isAtStart = true;
        quotesRemaining = meta.quoteCount;
        escapesRemaining = meta.escapeCount;
    }

    public readonly ReadOnlyMemory<T> GetAsMemory(ReadOnlySpan<T> field)
    {
        if (field.IsEmpty)
            return default;

        Debug.Assert(Record == _record.Span);

        if (field.Overlaps(Record, out int elementOffset))
        {
            return _record.Slice(-elementOffset, field.Length);
        }

        if (!Unsafe.IsNullRef(ref _unescapeArray) &&
            _unescapeArray != null &&
            field.Overlaps(_unescapeArray.AsSpan(), out elementOffset))
        {
            return _unescapeArray.AsMemory(elementOffset, field.Length);
        }

        return ThrowForInvalidGetAsMemory();
    }

    private readonly ReadOnlyMemory<T> ThrowForInvalidGetAsMemory()
    {
        string message;

        if (Unsafe.IsNullRef(ref _unescapeArray))
        {
            message = "unescapeArray was nullref";
        }
        else if (_unescapeArray is null)
        {
            message = "unescapeArray was null";
        }
        else
        {
            message = "field was not constructed from unescapeArray";
        }

        throw new InvalidOperationException(
            $"GetAsMemory failed, field was not from the original buffer, and {message}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ref T GetRemainingRef(out nuint remaining)
    {
        remaining = (uint)Record.Length - (uint)Consumed;
        return ref Unsafe.Add(ref MemoryMarshal.GetReference(Record), Consumed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Span<T> GetUnescapeBuffer(int length)
    {
        Debug.Assert(length != 0);

        if (length <= _unescapeBuffer.Length)
            return _unescapeBuffer.Slice(0, length);

        _options._arrayPool.EnsureCapacity(ref _unescapeArray, length);
        return _unescapeArray.AsSpan(0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void EnsureFullyConsumed(int fieldCount)
    {
        if ((((uint)Record.Length - (uint)Consumed) | quotesRemaining | escapesRemaining) == 0)
            return;

        ThrowNotFullyConsumed(fieldCount);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowParseFailed(ReadOnlySpan<T> field, CsvConverter<T>? parser)
    {
        string withStr = parser is null ? "" : $" with {parser.GetType()}";

        throw new CsvParseException(
            $"Failed to parse{withStr} from {_options.AsPrintableString(field.ToArray())}.")
        { Parser = parser };
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowForInvalidEOF()
    {
        string escapeStr = !Escape.HasValue ? "" : $"and {escapesRemaining} escapes ";
        throw new UnreachableException(
            $"The record ended while having {quotesRemaining} quotes {escapeStr}remaining. " +
            $"Record: {_options.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal readonly void ThrowForInvalidEndOfString()
    {
        throw new UnreachableException(
            "The record had a string that ended in the middle without the next character being a delimiter. " +
            $"Record: {_options.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal readonly void ThrowEscapeAtEnd()
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (escape token was the final character), " +
            $"Remaining: {_options.AsPrintableString(_record.Slice(Consumed))}, " +
            $"Record: {_options.AsPrintableString(_record)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal readonly void ThrowNoDelimiterAtHead()
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (no delimiter at head after the first field), " +
            $"Remaining: {_options.AsPrintableString(_record.Slice(Consumed))}, " +
            $"Record: {_options.AsPrintableString(_record)}");
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

        if (!End)
        {
            if (fieldCount != -1)
            {
                sb.Append(CultureInfo.InvariantCulture, $"Expected the record to have {fieldCount} fields, but it had more. ");
            }
            sb.Append(CultureInfo.InvariantCulture, $"Remaining: {_options.AsPrintableString(_record.Slice(Consumed))}, ");
        }

        sb.Append(CultureInfo.InvariantCulture, $"Record: {_options.AsPrintableString(_record)}");

        throw new CsvFormatException(sb.ToString());
    }
}
