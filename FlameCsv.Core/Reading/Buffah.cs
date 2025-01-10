using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;

// ReSharper disable DefaultStructEqualityIsUsed.Global

#pragma warning disable CS0660, CS0661

// ReSharper disable InlineTemporaryVariable

#pragma warning disable RCS1158
namespace FlameCsv.Reading;

[DebuggerDisplay("Start: {Start}, Length: {Length}, Quotes: {QuotesRemaining}, IsEOL: {IsEOL}")]
public readonly struct Meta
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Meta(int start, int length, uint quotesRemaining, bool isEOL)
    {
        Debug.Assert(start >= 0 && length >= 0);
        _start = isEOL ? start | int.MinValue : start;
        Length = length;
        QuotesRemaining = quotesRemaining;
    }

    private readonly int _start;

    public int Start => _start & ~int.MinValue;
    public int Length { get; }
    public uint QuotesRemaining { get; }
    public bool IsEOL => (_start & int.MinValue) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetStartOfNext(int newlineLength) => Start + Length + (IsEOL ? newlineLength : 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> SliceUnsafe<T>(
        ref readonly CsvDialect<T> dialect,
        ReadOnlySpan<T> data,
        Span<T> buffer)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (QuotesRemaining <= 2)
        {
            return MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(data), Start + (QuotesRemaining != 0 ? 1 : 0)),
                unchecked(Length - (int)QuotesRemaining));
        }

        Debug.Assert(QuotesRemaining % 2 == 0);

        int unescapedLength = Length - (int)(QuotesRemaining / 2u) - 2;

        if (buffer.Length < unescapedLength)
        {
            Throw.Argument(nameof(buffer), "Not enough space to unescape");
        }

        RFC4180Mode<T>.Unescape(dialect.Quote, buffer, data.Slice(Start + 1, Length - 2), QuotesRemaining - 2);
        return buffer[..unescapedLength];
    }
}

public static class Buffah<T> where T : unmanaged, IBinaryInteger<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Read(
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBufferSpan,
        ref readonly CsvDialect<T> dialect,
        SearchValues<T> anyToken,
        bool isFinalBlock)
    {
#if !DEBUG
        if (Unsafe.SizeOf<T>() == sizeof(char))
        {
            return Buffah<ushort>.ReadCore(
                MemoryMarshal.Cast<T, ushort>(data),
                metaBufferSpan,
                ref Unsafe.As<CsvDialect<T>, CsvDialect<ushort>>(ref Unsafe.AsRef(in dialect)),
                Unsafe.As<SearchValues<ushort>>(anyToken),
                isFinalBlock);
        }
#endif

        return ReadCore(data, metaBufferSpan, in dialect, anyToken, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int ReadCore(
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBufferSpan,
        ref readonly CsvDialect<T> dialect,
        SearchValues<T> anyToken,
        bool isFinalBlock)
    {
        int linesRead = 0;
        bool eol = false;
        uint quotesConsumed = 0;
        ReadOnlySpan<T> currentStart = data;
        ReadOnlySpan<T> remaining = data;
        var newline = NewlineBuffer<T>.CRLF;
        var searchValues = anyToken;
        scoped Span<Meta> metaBuffer = metaBufferSpan;

        while (linesRead < metaBuffer.Length)
        {
            int index = (quotesConsumed & 1) == 0
                ? remaining.IndexOfAny(searchValues)
                : remaining.IndexOf(dialect.Quote);

            if (index == -1)
            {
                break;
            }

            T token = remaining[index];

            if (token == dialect.Quote)
            {
                quotesConsumed++;
                remaining = remaining.Slice(index + 1);
                continue;
            }

            // cannot be inside a string in this case
            if (token == dialect.Delimiter)
            {
                goto FoundDelimiter;
            }

            if (token == newline.First &&
                (newline.Length == 1 || (remaining.Length >= index + 1 && remaining[index + 1] == newline.Second)))
            {
                goto FoundEOL;
            }

            remaining = remaining.Slice(index + 1);
            continue;

        FoundEOL:
            eol = true;
        FoundDelimiter:
            int start = GetOffset(data, currentStart);
            int length = currentStart.Length - remaining.Length + index;

            if (quotesConsumed != 0)
            {
                Debug.Assert(length >= 2);
                Debug.Assert(quotesConsumed % 2 == 0);

                if (MemoryMarshal.GetReference(currentStart) != dialect.Quote ||
                    Unsafe.Add(ref MemoryMarshal.GetReference(currentStart), length - 1) != dialect.Quote)
                {
                    Throw.InvalidOperation("Invalid state");
                }
            }

            metaBuffer[linesRead++] = new Meta(
                start: start,
                length: length,
                quotesRemaining: quotesConsumed,
                isEOL: eol);

            remaining = remaining.Slice(index + (eol ? newline.Length : 1));
            currentStart = remaining;
            quotesConsumed = 0;
            eol = false;
        }

        if (isFinalBlock && !currentStart.IsEmpty && linesRead < metaBuffer.Length)
        {
            metaBuffer[linesRead++] = new Meta(
                start: GetOffset(data, currentStart),
                length: currentStart.Length,
                quotesRemaining: quotesConsumed,
                isEOL: true);
        }

        return linesRead;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOffset(ReadOnlySpan<T> data, ReadOnlySpan<T> remaining)
    {
        ref T dataStart = ref MemoryMarshal.GetReference(data);
        ref T dataEnd = ref MemoryMarshal.GetReference(remaining);
        nint offset = Unsafe.ByteOffset(ref dataStart, ref dataEnd);
        return (int)(offset / Unsafe.SizeOf<T>());
    }
}
