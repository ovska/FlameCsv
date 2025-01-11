using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using FlameCsv.Extensions;

#pragma warning disable RCS1158
namespace FlameCsv.Reading;

internal interface INewline<T> where T : unmanaged, IBinaryInteger<T>
{
    int Length { get; }
    nuint OffsetFromEnd { get; }
    bool Equals(ref T value);
}

internal readonly struct NewlineSingle<T>(T first) : INewline<T> where T : unmanaged, IBinaryInteger<T>
{
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1;
    }

    public nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ref T value) => value == first;
}

internal readonly struct NewlineDouble<T> : INewline<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly T _first;
    private readonly T _second;

    public NewlineDouble(T first, T second)
    {
        _first = first;
        _second = second;
    }


    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 2;
    }

    public nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ref T value) => value == _first && Unsafe.Add(ref value, 1) == _second;
}

[DebuggerDisplay("End: {End}, Quotes: {QuoteCount}, IsEOL: {IsEOL}")]
public readonly struct Meta
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Meta(int end, uint quoteCount, bool isEOL)
    {
        _end = isEOL ? end | int.MinValue : end;
        QuoteCount = quoteCount;
    }

    private readonly int _end;

    public int End => _end & ~int.MinValue;
    public uint QuoteCount { get; }
    public bool IsEOL => (_end & int.MinValue) != 0;

    /// <summary>
    /// Returns length with the trailing newline or comma included.
    /// </summary>
    /// <param name="newlineLength"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetLength(int newlineLength) => End + (IsEOL ? newlineLength : 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> SliceUnsafe<T>(
        int runningIndex,
        ref readonly CsvDialect<T> dialect,
        ReadOnlySpan<T> data,
        Span<T> buffer)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (QuoteCount <= 2)
        {
            int startOffset = QuoteCount != 0 ? 1 : 0;
            return data.Slice(runningIndex + startOffset, End - (int)QuoteCount);
        }

        throw new NotImplementedException();

        // Debug.Assert(QuotesRemaining % 2 == 0);
        //
        // int unescapedLength = Length - (int)(QuotesRemaining / 2u) - 2;
        //
        // if (buffer.Length < unescapedLength)
        // {
        //     Throw.Argument(nameof(buffer), "Not enough space to unescape");
        // }
        //
        // RFC4180Mode<T>.Unescape(dialect.Quote, buffer, data.Slice(Start + 1, Length - 2), QuotesRemaining - 2);
        // return buffer[..unescapedLength];
    }
}

internal static class Buffah<T> where T : unmanaged, IBinaryInteger<T>
{
    public readonly ref struct State<TSimd, TVector>
        where TSimd : struct, ISimdVector<T, TVector>
        where TVector : struct
    {
        public readonly T Delimiter;
        public readonly T Quote;
        public readonly TVector DelimiterVec;
        public readonly TVector QuoteVec;
        public readonly TVector Newline1Vec;
        public readonly TVector Newline2Vec;
        public readonly ref readonly NewlineBuffer<T> Newline;

        public State(ref readonly CsvDialect<T> dialect, ref readonly NewlineBuffer<T> newline)
        {
            Delimiter = dialect.Delimiter;
            Quote = dialect.Quote;
            DelimiterVec = TSimd.Create(dialect.Delimiter);
            QuoteVec = TSimd.Create(dialect.Quote);
            Newline1Vec = TSimd.Create(newline.First);
            Newline2Vec = TSimd.Create(newline.Second);
            Newline = ref newline;
        }
    }

    public static int Read(
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBufferSpan,
        ref readonly CsvDialect<T> dialect,
        bool isFinalBlock)
    {
#if true || !DEBUG
        if (Unsafe.SizeOf<T>() == sizeof(char))
        {
            return Buffah<ushort>.ReadCore(
                MemoryMarshal.Cast<T, ushort>(data),
                metaBufferSpan,
                ref Unsafe.As<CsvDialect<T>, CsvDialect<ushort>>(ref Unsafe.AsRef(in dialect)),
                isFinalBlock);
        }
#endif

        return ReadCore(data, metaBufferSpan, in dialect, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadCore(
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBufferSpan,
        ref readonly CsvDialect<T> dialect,
        bool isFinalBlock)
    {
        if (Vec512<T>.IsSupported)
        {
            return ReadImpl<Vec512<T>, Vector512<T>>(
                new(in dialect, in NewlineBuffer<T>.CRLF),
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        if (Vec256<T>.IsSupported)
        {
            return ReadImpl<Vec256<T>, Vector256<T>>(
                new(in dialect, in NewlineBuffer<T>.CRLF),
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        if (Vec128<T>.IsSupported)
        {
            return ReadImpl<Vec128<T>, Vector128<T>>(
                new(in dialect, in NewlineBuffer<T>.CRLF),
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        if (Vec64<T>.IsSupported)
        {
            return ReadImpl<Vec64<T>, Vector64<T>>(
                new(in dialect, in NewlineBuffer<T>.CRLF),
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        throw new NotImplementedException();
        // return ReadCore(data, metaBufferSpan, in dialect, anyToken, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    [SuppressMessage("ReSharper", "InlineTemporaryVariable")]
    private static int ReadImpl<TSimd, TVector>(
        in State<TSimd, TVector> state,
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBuffer,
        bool isFinalBlock)
        where TSimd : struct, ISimdVector<T, TVector>
        where TVector : struct
    {
        Debug.Assert(!data.IsEmpty);
        Debug.Assert(!metaBuffer.IsEmpty);

        bool eol = false;

        TVector delimiterVec = state.DelimiterVec;
        TVector quoteVec = state.QuoteVec;
        TVector newline1Vec = state.Newline1Vec;
        TVector newline2Vec = state.Newline2Vec;

        // search space of T is set to 1 vector less, possibly leaving space for a newline token so we don't need
        // to do bounds checks in the loops
        ref readonly T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = 0;
        nuint searchSpaceEnd = (nuint)data.Length - (nuint)TSimd.Count - (nuint)(state.Newline.Length - 1);

        // search space of Meta is set to vector length from actual so we don't need to do bounds checks in the loops
        ref Meta currentMeta = ref MemoryMarshal.GetReference(metaBuffer);
        ref readonly Meta metaEnd = ref Unsafe.Add(
            ref MemoryMarshal.GetReference(metaBuffer),
            metaBuffer.Length - TSimd.Count);

        nuint quotesConsumed = 0;

        while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd))
        {
            while (runningIndex <= searchSpaceEnd)
            {
                TVector vector = TSimd.LoadUnsafe(in first, runningIndex);

                TVector hasDelimiter = TSimd.Equals(vector, delimiterVec);
                TVector hasQuote = TSimd.Equals(vector, quoteVec);
                TVector hasNewline = TSimd.Or(TSimd.Equals(vector, newline1Vec), TSimd.Equals(vector, newline2Vec));
                TVector hasNewlineOrDelimiter = TSimd.Or(hasNewline, hasDelimiter);
                TVector hasAny = TSimd.Or(hasQuote, hasNewlineOrDelimiter);

#if DEBUG
                // @formatter:off
                var _metas = metaBuffer[..(((int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta)) / Unsafe.SizeOf<Meta>())];
                var _runningIndex = (ulong)runningIndex;
                var _currentVector = DebugExt.AsString(vector);
                var _afterCurrent = data.Slice((int)runningIndex + TSimd.Count).UnsafeCast<T, char>().ToString();
                // @formatter:on
#endif

                nuint maskAny = TSimd.ExtractMostSignificantBits(hasAny);

                // nothing of note in this slice
                if (maskAny == 0)
                {
                    goto NextLoop;
                }

                nuint maskDelimiter = TSimd.ExtractMostSignificantBits(hasDelimiter);

                // only delimiters
                if (maskDelimiter == maskAny)
                {
                    currentMeta = ref ParseDelimiters(maskDelimiter, (int)runningIndex, ref currentMeta);
                    goto NextLoop;
                }

                nuint maskNewlineOrDelimiter = TSimd.ExtractMostSignificantBits(hasNewlineOrDelimiter);

                if (maskNewlineOrDelimiter == maskAny)
                {
                    if (maskDelimiter == 0)
                    {
                        currentMeta = ref ParseLineEnds(
                            maskNewlineOrDelimiter,
                            ref Unsafe.AsRef(in first),
                            (int)runningIndex,
                            ref currentMeta,
                            in state.Newline);
                    }
                    else
                    {
                        // currentMeta = ref ParseDelimitersAndLineEnds(
                        //     maskDelimiter,
                        //     maskNewlineOrDelimiter,
                        //     (int)runningIndex,
                        //     ref currentMeta,
                        //     in metaEnd);
                    }
                }

            NextLoop:
                runningIndex += (nuint)TSimd.Count;
            }

            // TODO: read one by one the rest?
            break;
        }

        return (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta) /
            Unsafe.SizeOf<Meta>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimiters(
        nuint mask,
        int currentIndex,
        ref Meta currentMeta)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            currentMeta = new(currentIndex + offset, 0, false);
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
        } while (mask != 0); // see comment about metaEnd

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseLineEnds(
        nuint mask,
        ref T first,
        int currentIndex,
        ref Meta currentMeta,
        ref readonly NewlineBuffer<T> newline)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);

            if (newline.First == Unsafe.Add(ref first, currentIndex + offset))
            {
                // searchspace is set to (len - vector count - 1) to ensure we can always peek one address
                // ahead for the second newline token
                if (newline.Length == 1 ||
                    (newline.Second == Unsafe.Add(ref first, currentIndex + offset + 1)))
                {
                    currentMeta = new(currentIndex + offset, 0, isEOL: true);
                    currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                }
            }
        } while (mask != 0); // see comment about metaEnd

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimitersAndLineEnds(
        nuint mask,
        int currentIndex,
        ref Meta currentMeta,
        T delimiter,
        ref readonly NewlineBuffer<T> newline)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);

            currentMeta = new(currentIndex + offset, 0, false);
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
        } while (mask != 0); // see comment about metaEnd

        return ref currentMeta;
    }
}

#if DEBUG
file static class DebugExt
{
    public static string AsString(object? value)
    {
        Span<ushort> buffer = stackalloc ushort[256];
        int count = 0;

        if (value is Vector512<ushort> v512)
        {
            v512.CopyTo(buffer);
            count = Vector512<ushort>.Count;
        }

        if (value is Vector256<ushort> v256)
        {
            v256.CopyTo(buffer);
            count = Vector256<ushort>.Count;
        }

        if (value is Vector128<ushort> v128)
        {
            v128.CopyTo(buffer);
            count = Vector128<ushort>.Count;
        }

        if (value is Vector64<ushort> v64)
        {
            v64.CopyTo(buffer);
            count = Vector64<ushort>.Count;
        }

        return buffer.UnsafeCast<ushort, char>()[..count].ToString();
    }
}
#endif
