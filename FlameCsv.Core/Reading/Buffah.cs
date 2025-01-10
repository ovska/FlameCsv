using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using FlameCsv.Extensions;

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
    public static int Read(
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBuffer,
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

        while (linesRead < metaBuffer.Length)
        {
            if (remaining.IsEmpty)
            {
                break;
            }

            int index = (quotesConsumed & 1) == 0
                ? remaining.IndexOfAny(anyToken)
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

internal interface IVector<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct
{
    static abstract bool IsSupported { get; }
    static abstract int Count { get; }
    static abstract TVector Equals(TVector left, TVector right);
    static abstract TVector Create(T value);
    static abstract TVector LoadUnsafe(ref readonly T source, nuint length);
    static abstract uint ExtractMostSignificantBits(TVector value);
}

internal readonly struct Vector64Impl<T> : IVector<T, Vector64<T>> where T : unmanaged, IBinaryInteger<T>
{
    public static bool IsSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector64<T>.IsSupported && Vector64.IsHardwareAccelerated;
    }

    public static int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector64<T>.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector64<T> Equals(Vector64<T> left, Vector64<T> right) => Vector64.Equals(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector64<T> Create(T value) => Vector64.Create(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector64<T> LoadUnsafe(ref readonly T source, nuint length)
        => Vector64.LoadUnsafe(in source, (nuint)Unsafe.SizeOf<T>() * length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ExtractMostSignificantBits(Vector64<T> value) => value.ExtractMostSignificantBits();
}
