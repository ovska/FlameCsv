﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using FlameCsv.Extensions;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

internal interface INewline<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns the length of the newline sequence.
    /// </summary>
    static abstract int Length { get; }

    /// <summary>
    /// Returns the offset required in the search space to be able to read the whole newline value.
    /// </summary>
    static abstract nuint OffsetFromEnd { get; }

    /// <summary>
    /// Clears the second bit in the mask if needed. No-op for single newline sequences.
    /// </summary>
    /// <param name="mask">The mask to modify.</param>
    static abstract void ClearSecondBitIfNeeded(ref nuint mask);

    /// <summary>
    /// Determines if the specified value represents a newline.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <remarks>
    /// Can read the next value in the sequence if needed, see <see cref="OffsetFromEnd"/>.
    /// </remarks>
    /// <returns>True if the value represents a newline; otherwise, false.</returns>
    [Pure]
    bool IsNewline(ref T value);

    /// <summary>
    /// Determines if the specified value represents a delimiter or a newline.
    /// </summary>
    /// <param name="delimiter">Delimiter</param>
    /// <param name="value">The value to check against</param>
    /// <param name="isEOL">When true, whether the value was a newline instead of delimiter</param>
    /// <returns>True if the value represents a delimiter or a newline; otherwise, false.</returns>
    /// <remarks>For single token newlines, always returns true</remarks>
    [Pure]
    bool IsDelimiterOrNewline(T delimiter, ref T value, out bool isEOL);
}

internal interface INewline<T, TVector> : INewline<T>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct
{
    /// <summary>
    /// Determines if the input vector contains any token of the newline.
    /// </summary>
    /// <param name="input">The input vector to check.</param>
    /// <returns>A vector indicating the positions of newline sequences.</returns>
    [Pure]
    TVector HasNewline(TVector input);
}

internal readonly ref struct NewlineSingle<T, TVector>(T first) : INewline<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TVector HasNewline(TVector input) => TVector.Equals(input, _firstVec);

    private readonly TVector _firstVec = TVector.Create(first);

    public static int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => 1;
    }

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNewline(ref T value) => value == first;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDelimiterOrNewline(T delimiter, ref T value, out bool isEOL)
    {
        Debug.Assert(value == delimiter || value == first);
        isEOL = value != delimiter;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearSecondBitIfNeeded(ref nuint mask)
    {
        // no-op
    }
}

internal readonly ref struct NewlineDouble<T, TVector>(T first, T second) : INewline<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TVector HasNewline(TVector input)
        => TVector.Or(TVector.Equals(input, _firstVec), TVector.Equals(input, _secondVec));

    private readonly TVector _firstVec = TVector.Create(first);
    private readonly TVector _secondVec = TVector.Create(second);

    public static int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => 2;
    }

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNewline(ref T value) => value == first && Unsafe.Add(ref value, 1) == second;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDelimiterOrNewline(T delimiter, ref T value, out bool isEOL)
    {
        if (delimiter == value)
        {
            isEOL = false;
            return true;
        }

        isEOL = true;
        return value == first && Unsafe.Add(ref value, 1) == second;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearSecondBitIfNeeded(ref nuint mask)
    {
        mask &= (mask - 1); // TODO: does not work properly?
    }
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
    }
}

internal static class Buffah<T> where T : unmanaged, IBinaryInteger<T>
{
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
            NewlineDouble<T, Vec512<T>> newline = new(T.CreateSaturating('\r'), T.CreateSaturating('\n'));
            return ReaderImpl<T, NewlineDouble<T, Vec512<T>>, Vec512<T>>.Core(
                in dialect,
                in newline,
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        if (Vec256<T>.IsSupported)
        {
            NewlineDouble<T, Vec256<T>> newline = new(T.CreateSaturating('\r'), T.CreateSaturating('\n'));
            return ReaderImpl<T, NewlineDouble<T, Vec256<T>>, Vec256<T>>.Core(
                in dialect,
                in newline,
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        if (Vec128<T>.IsSupported)
        {
            NewlineDouble<T, Vec128<T>> newline = new(T.CreateSaturating('\r'), T.CreateSaturating('\n'));
            return ReaderImpl<T, NewlineDouble<T, Vec128<T>>, Vec128<T>>.Core(
                in dialect,
                in newline,
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        if (Vec64<T>.IsSupported)
        {
            NewlineDouble<T, Vec64<T>> newline = new(T.CreateSaturating('\r'), T.CreateSaturating('\n'));
            return ReaderImpl<T, NewlineDouble<T, Vec64<T>>, Vec64<T>>.Core(
                in dialect,
                in newline,
                data,
                metaBufferSpan,
                isFinalBlock);
        }

        throw new NotImplementedException();
    }
}

internal static class ReaderImpl<T, TNewline, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline<T, TVector>, allows ref struct
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    public static int Core(
        scoped ref readonly CsvDialect<T> dialect,
        scoped ref readonly TNewline newline,
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBuffer,
        bool isFinalBlock)
    {
        Debug.Assert(!data.IsEmpty);
        Debug.Assert(!metaBuffer.IsEmpty);

        T delimiter = dialect.Delimiter;
        T quote = dialect.Quote;

        TVector delimiterVec = TVector.Create(delimiter);
        TVector quoteVec = TVector.Create(quote);

        // search space of T is set to 1 vector less, possibly leaving space for a newline token so we don't need
        // to do bounds checks in the loops
        ref readonly T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = 0;
        nuint searchSpaceEnd = (nuint)data.Length - (nuint)TVector.Count - TNewline.OffsetFromEnd;

        // if (typeof(T) == typeof(byte))
        // {
        //     runningIndex = Math.Min(UnsafeAccess.IndexOfFirstNonAsciiByte(data.UnsafeCast<T, byte>()), searchSpaceEnd);
        // }

        // search space of Meta is set to vector length from actual so we don't need to do bounds checks in the loops
        ref Meta currentMeta = ref MemoryMarshal.GetReference(metaBuffer);
        ref readonly Meta metaEnd = ref Unsafe.Add(
            ref MemoryMarshal.GetReference(metaBuffer),
            metaBuffer.Length - TVector.Count);

        uint quotesConsumed = 0;

        while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd) && runningIndex <= searchSpaceEnd)
        {
            TVector vector = TVector.LoadUnsafe(in first, runningIndex);

            TVector hasDelimiter = TVector.Equals(vector, delimiterVec);
            TVector hasQuote = TVector.Equals(vector, quoteVec);
            TVector hasNewline = newline.HasNewline(vector);
            TVector hasNewlineOrDelimiter = TVector.Or(hasNewline, hasDelimiter);
            TVector hasAny = TVector.Or(hasQuote, hasNewlineOrDelimiter);

            nuint maskAny = hasAny.ExtractMostSignificantBits();

            // nothing of note in this slice
            if (maskAny == 0)
            {
                runningIndex += (nuint)TVector.Count;
                continue;
            }

            if (typeof(T) == typeof(byte)&& !Ascii.IsValid(data.UnsafeCast<T,byte>().Slice((int)runningIndex, TVector.Count)))
            {

            }

            nuint maskDelimiter = hasDelimiter.ExtractMostSignificantBits();

            // only delimiters? skip this if there are any quotes in the current field
            if (quotesConsumed == 0u && maskDelimiter == maskAny)
            {
                Debug.Assert(quotesConsumed == 0);
                currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);
                runningIndex += (nuint)TVector.Count;
                continue;
            }

            nuint maskNewlineOrDelimiter = hasNewlineOrDelimiter.ExtractMostSignificantBits();

            if (quotesConsumed == 0u && maskNewlineOrDelimiter == maskAny)
            {
                if (maskDelimiter != 0)
                {
                    nuint maskNewline = maskNewlineOrDelimiter & ~maskDelimiter;
                    int indexNewline = BitOperations.TrailingZeroCount(maskNewline);

                    // check if the delimiters and newlines are interleaved
                    if ((Unsafe.SizeOf<nuint>() * 8 - 1) - BitOperations.LeadingZeroCount(maskDelimiter) <
                        indexNewline)
                    {
                        // all delimiters are before any of the newlines
                        Debug.Assert(quotesConsumed == 0);
                        currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);

                        // fall through to parse line ends
                        maskNewlineOrDelimiter = maskNewline;
                    }
                    else
                    {
                        Debug.Assert(quotesConsumed == 0);
                        currentMeta = ref ParseDelimitersAndLineEnds(
                            maskNewlineOrDelimiter,
                            ref Unsafe.AsRef(in first),
                            ref runningIndex,
                            ref currentMeta,
                            delimiter,
                            in newline);
                        runningIndex += (nuint)TVector.Count;
                        continue;
                    }
                }

                currentMeta = ref ParseLineEnds(
                    maskNewlineOrDelimiter,
                    ref Unsafe.AsRef(in first),
                    ref runningIndex,
                    ref currentMeta,
                    in newline);
            }
            else
            {
                // mixed delimiters, quotes, and newlines
                currentMeta = ref ParseAny(
                    maskAny,
                    ref Unsafe.AsRef(in first),
                    ref runningIndex,
                    ref currentMeta,
                    delimiter,
                    quote,
                    in newline,
                    ref quotesConsumed);
            }

            runningIndex += (nuint)TVector.Count;
        }

        // TODO: read one by one the rest
        return (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta) /
            Unsafe.SizeOf<Meta>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimiters(
        nuint mask,
        nuint runningIndex,
        ref Meta currentMeta)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            currentMeta = new((int)runningIndex + offset, 0, false);
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseLineEnds(
        nuint mask,
        ref T first,
        ref nuint runningIndex,
        ref Meta currentMeta,
        ref readonly TNewline newline)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            if (newline.IsNewline(ref Unsafe.Add(ref first, runningIndex + (nuint)offset)))
            {
                currentMeta = new((int)runningIndex + offset, 0, isEOL: true);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);

                // clear the second bit if needed
                TNewline.ClearSecondBitIfNeeded(ref mask);

                // adjust the index if we crossed a vector boundary
                if (TNewline.OffsetFromEnd != 0u && offset == TVector.Count - 1)
                {
                    runningIndex += TNewline.OffsetFromEnd;
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimitersAndLineEnds(
        nuint mask,
        ref T first,
        ref nuint runningIndex,
        ref Meta currentMeta,
        T delimiter,
        ref readonly TNewline newline)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            if (newline.IsDelimiterOrNewline(
                    delimiter,
                    ref Unsafe.Add(ref first, runningIndex + (nuint)offset),
                    out bool isEOL))
            {
                currentMeta = new((int)runningIndex + offset, 0, isEOL);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);

                // clear the second bit if needed
                if (isEOL)
                {
                    TNewline.ClearSecondBitIfNeeded(ref mask);

                    // adjust the index if we crossed a vector boundary
                    if (TNewline.OffsetFromEnd != 0u && offset == TVector.Count - 1)
                    {
                        runningIndex += TNewline.OffsetFromEnd;
                    }
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }

    private static ref Meta ParseAny(
        nuint mask,
        ref T first,
        ref nuint runningIndex,
        ref Meta currentMeta,
        T delimiter,
        T quote,
        ref readonly TNewline newline,
        ref uint quotesConsumed)
    {
        string _xyz = typeof(T) != typeof(byte)
            ? ""
            : Encoding.UTF8.GetString((MemoryMarshal.CreateSpan(ref Unsafe.Add(ref first, runningIndex), (int)TVector.Count)).UnsafeCast<T,byte>());

        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            if (Unsafe.Add(ref first, runningIndex + (nuint)offset) == quote)
            {
                ++quotesConsumed;
            }
            else if ((quotesConsumed & 1) == 0 &&
                     newline.IsDelimiterOrNewline(
                         delimiter,
                         ref Unsafe.Add(ref first, runningIndex + (nuint)offset),
                         out bool isEOL))
            {
                currentMeta = new((int)runningIndex + offset, quotesConsumed, isEOL);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                quotesConsumed = 0;

                // clear the second bit if needed
                if (isEOL)
                {
                    TNewline.ClearSecondBitIfNeeded(ref mask);

                    // adjust the index if we crossed a vector boundary
                    if (TNewline.OffsetFromEnd != 0u && offset == TVector.Count - 1)
                    {
                        runningIndex += TNewline.OffsetFromEnd;
                    }
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }
}

file static class UnsafeAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe nuint IndexOfFirstNonAsciiByte(ReadOnlySpan<byte> data)
    {
        fixed (byte* pBytes = &MemoryMarshal.GetReference(data))
        {
            return (nuint)GetCharCountFast(Encoding.ASCII, pBytes, data.Length, null, out _);
        }
    }

    // HACK: until we can use static Ascii.GetIndexOfFirstNonAsciiByte directly via UnsafeAccessor
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = nameof(GetCharCountFast))]
    private static extern unsafe int GetCharCountFast(
        Encoding encoding,
        byte* pBytes,
        int bytesLength,
        DecoderFallback? fallback,
        out int bytesConsumed);
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