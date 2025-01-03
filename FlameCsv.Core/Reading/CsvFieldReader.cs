using System.Collections;
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

public interface ICsvFieldReader<T> : IEnumerator<ReadOnlySpan<T>> where T : unmanaged, IEquatable<T>
{
    ReadOnlyMemory<T> RawRecord { get; }
    CsvOptions<T> Options { get; }
}

public ref struct CsvFieldReader<T> : ICsvFieldReader<T> where T : unmanaged, IEquatable<T>
{
    public readonly ReadOnlySpan<T> Record { get; }
    public readonly ReadOnlySpan<T> Remaining => Record.Slice(Consumed);
    public readonly bool End => Consumed >= Record.Length;

    public int Consumed { get; internal set; }

    public bool isAtStart;
    public uint quotesRemaining;
    public uint escapesRemaining;

    private readonly ref readonly CsvDialect<T> _dialect;
    public readonly T Delimiter => _dialect.Delimiter;
    public readonly T Quote => _dialect.Quote;
    public readonly T? Escape => _dialect.Escape;
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
        _dialect = ref options.Dialect;
        Whitespace = _dialect.Whitespace.Span;

        _record = record;

        _unescapeBuffer = unescapeBuffer;
        _unescapeArray = ref unescapeArray;

        isAtStart = true;
        quotesRemaining = meta.quoteCount;
        escapesRemaining = meta.escapeCount;
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

        if (Unsafe.IsNullRef(ref _unescapeArray))
        {
            Debug.Fail("Null ref unescape array");
            return GC.AllocateUninitializedArray<T>(length);
        }

        _options._arrayPool.EnsureCapacity(ref _unescapeArray, length);
        return _unescapeArray.AsSpan(0, length);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void ThrowParseFailed(ReadOnlySpan<T> field, CsvConverter<T>? parser)
    {
        string withStr = parser is null ? "" : $" with {parser.GetType()}";

        throw new CsvParseException(
            $"Failed to parse{withStr} from {_options.AsPrintableString(field.ToArray())}.")
        { Converter = parser };
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
    private readonly void ThrowNotFullyConsumed()
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
            sb.Append(CultureInfo.InvariantCulture, $"Remaining: {_options.AsPrintableString(_record.Slice(Consumed))}, ");
        }

        sb.Append(CultureInfo.InvariantCulture, $"Record: {_options.AsPrintableString(_record)}");

        throw new CsvFormatException(sb.ToString());
    }

    public ReadOnlySpan<T> Current { get; private set; }

    public bool MoveNext()
    {
        if (!End)
        {
            Current = !Escape.HasValue
                ? RFC4180Mode<T>.ReadNextField(ref this)
                : UnixMode<T>.ReadNextField(ref this);
            return true;
        }

        Current = default;
        return false;
    }

    public readonly void Dispose()
    {
        if ((((uint)Record.Length - (uint)Consumed) | quotesRemaining | escapesRemaining) == 0)
            return;

        ThrowNotFullyConsumed();
    }

    public readonly ReadOnlyMemory<T> RawRecord => _record;
    public readonly CsvOptions<T> Options => _options;

    readonly void IEnumerator.Reset() => throw new NotSupportedException();
    readonly object IEnumerator.Current => throw new NotSupportedException();
}
