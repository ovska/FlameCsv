using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal static partial class Field
{
[SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Unescape<T>(T quote, scoped Span<T> buffer, ReadOnlySpan<T> field, uint quotesConsumed)
        where T : unmanaged, IBinaryInteger<T>
    {
        Debug.Assert(quotesConsumed >= 2);
        Debug.Assert(quotesConsumed % 2 == 0);
        Debug.Assert(field.Length >= 2);
        Debug.Assert(field.Length >= quotesConsumed);
        Debug.Assert(buffer.Length >= field.Length);

        uint quotePairsRemaining = quotesConsumed / 2;

        // leave 1 space for the second quote
        uint len = (uint)field.Length;

        scoped ref T src = ref MemoryMarshal.GetReference(field);
        scoped ref T dst = ref MemoryMarshal.GetReference(buffer);
        scoped ref T srcEnd = ref Unsafe.Add(ref src, len);
        scoped ref T oneFromEnd = ref Unsafe.Subtract(ref srcEnd, 1);

        // TODO: optimized version for AVX512
        if (Vector.IsHardwareAccelerated && len >= Vector<T>.Count)
        {
            Vector<T> quoteVector = Vector.Create(quote);
            scoped ref T vectorFromEnd = ref Unsafe.Subtract(ref srcEnd, Vector<T>.Count);

            do
            {
                Vector<T> vec = Vector.LoadUnsafe(ref src);
                Vector<T> equals = Vector.Equals(vec, quoteVector);
                vec.StoreUnsafe(ref dst); // eager store

                if (equals == Vector<T>.Zero)
                {
                    src = ref Unsafe.Add(ref src, Vector<T>.Count);
                    dst = ref Unsafe.Add(ref dst, Vector<T>.Count);
                    continue;
                }

                nuint charpos = IndexOfFirstNonZero(equals) + 1u;
                src = ref Unsafe.Add(ref src, charpos);
                dst = ref Unsafe.Add(ref dst, charpos);

                // check if the next is a quote, or dangling quote at the end
                if (Unsafe.IsAddressGreaterThanOrEqualTo(ref src, ref srcEnd) || src != quote)
                {
                    goto Fail;
                }

                src = ref Unsafe.Add(ref src, 1);

                if (--quotePairsRemaining == 0)
                {
                    goto NoQuotesLeft;
                }
            } while (Unsafe.IsAddressLessThanOrEqualTo(ref src, ref vectorFromEnd));
        }
        else if (!Vector.IsHardwareAccelerated) // only use scalar if no vector support
        {
            ReadScalar:
            scoped ref T unrolledEnd = ref Unsafe.Subtract(ref srcEnd, 8);

            while (Unsafe.IsAddressLessThan(ref src, ref unrolledEnd))
            {
                // csharpier-ignore
                {
                    if (quote == Unsafe.Add(ref src, 0)) goto Found1;
                    if (quote == Unsafe.Add(ref src, 1)) goto Found2;
                    if (quote == Unsafe.Add(ref src, 2)) goto Found3;
                    if (quote == Unsafe.Add(ref src, 3)) goto Found4;
                    if (quote == Unsafe.Add(ref src, 4)) goto Found5;
                    if (quote == Unsafe.Add(ref src, 5)) goto Found6;
                    if (quote == Unsafe.Add(ref src, 6)) goto Found7;
                    if (quote == Unsafe.Add(ref src, 7)) goto Found8;
                }

                Copy(ref src, ref dst, 8 * (uint)Unsafe.SizeOf<T>());
                src = ref Unsafe.Add(ref src, 8);
                dst = ref Unsafe.Add(ref dst, 8);
            }
            goto ReadTail;

            // JIT will optimize the copies with constant length to register moves
            Found1:
            Copy(ref src, ref dst, 1 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 1);
            dst = ref Unsafe.Add(ref dst, 1);
            goto FoundLong;
            Found2:
            Copy(ref src, ref dst, 2 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 2);
            dst = ref Unsafe.Add(ref dst, 2);
            goto FoundLong;
            Found3:
            Copy(ref src, ref dst, 3 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 3);
            dst = ref Unsafe.Add(ref dst, 3);
            goto FoundLong;
            Found4:
            Copy(ref src, ref dst, 4 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 4);
            dst = ref Unsafe.Add(ref dst, 4);
            goto FoundLong;
            Found5:
            Copy(ref src, ref dst, 5 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 5);
            dst = ref Unsafe.Add(ref dst, 5);
            goto FoundLong;
            Found6:
            Copy(ref src, ref dst, 6 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 6);
            dst = ref Unsafe.Add(ref dst, 6);
            goto FoundLong;
            Found7:
            Copy(ref src, ref dst, 7 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 7);
            dst = ref Unsafe.Add(ref dst, 7);
            goto FoundLong;
            Found8:
            Copy(ref src, ref dst, 8 * (uint)Unsafe.SizeOf<T>());
            src = ref Unsafe.Add(ref src, 8);
            dst = ref Unsafe.Add(ref dst, 8);
            goto FoundLong;

            FoundLong:
            // check if the next is a quote, or dangling quote at the end
            if (Unsafe.IsAddressGreaterThanOrEqualTo(ref src, ref srcEnd) || src != quote)
            {
                goto Fail;
            }

            // srcIndex++;
            src = ref Unsafe.Add(ref src, 1);

            if (--quotePairsRemaining == 0)
            {
                goto NoQuotesLeft;
            }

            goto ReadScalar;
        }

        ReadTail:
        while (Unsafe.IsAddressLessThan(ref src, ref oneFromEnd)) // leave space so we can check for the second quote
        {
            if (quote == src)
            {
                src = ref Unsafe.Add(ref src, 1);

                if (src != quote)
                {
                    goto Fail;
                }

                if (--quotePairsRemaining == 0)
                {
                    goto NoQuotesLeft;
                }
            }

            dst = src;
            src = ref Unsafe.Add(ref src, 1);
            dst = ref Unsafe.Add(ref dst, 1);
        }

        T last = src;

        if (quotePairsRemaining == 0 && last != quote) // can't end on a lone quote
        {
            dst = last;
            return;
        }

        Fail:
        ThrowInvalidUnescape(field, quote, quotesConsumed);

        // Copy remaining data
        NoQuotesLeft:
        Debug.Assert(quotePairsRemaining == 0);
        uint tailBytes = (uint)Unsafe.ByteOffset(ref src, ref srcEnd);
        Copy(ref src, ref dst, tailBytes);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, ref T dst, uint bytes)
        {
            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref dst),
                source: ref Unsafe.As<T, byte>(ref src),
                byteCount: bytes
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint IndexOfFirstNonZero(Vector<T> vec)
        {
            if (!AdvSimd.IsSupported)
            {
                return (uint)BitOperations.TrailingZeroCount(vec.MoveMask());
            }

            uint bitSize = (uint)Unsafe.SizeOf<T>() * 8u; // JIT constant folds

            Vector128<ulong> v128 = vec.AsVector128().AsUInt64();

            ulong lo = v128.GetElement(0);
            ulong hi = v128.GetElement(1);

            // relatively predictable branch
            if (lo != 0)
            {
                return (uint)BitOperations.TrailingZeroCount(lo) / bitSize;
            }

            uint hiIdx = (uint)BitOperations.TrailingZeroCount(hi) / bitSize;
            return (64u / bitSize) + hiIdx;
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidUnescape<T>(ReadOnlySpan<T> field, T quote, uint quoteCount)
        where T : unmanaged, IBinaryInteger<T>
    {
        int actualCount = field.Count(quote);

        var error = new StringBuilder(64);

        if (field.Length < 2)
        {
            error.Append(CultureInfo.InvariantCulture, $"Source is too short (length: {field.Length}). ");
        }

        if (actualCount != quoteCount)
        {
            error.Append(
                CultureInfo.InvariantCulture,
                $"String delimiter count {quoteCount} was invalid (actual was {actualCount}). "
            );
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in field)
        {
            error.Append(token.Equals(quote) ? '"' : 'x');
        }

        error.Append(']');

        throw new CsvFormatException($"Failed to unescape invalid data: {error}");
    }
}

#if !NET10_0_OR_GREATER
file static class Polyfill
{
    extension(Unsafe)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressGreaterThanOrEqualTo<T>(ref T left, ref T right)
            where T : unmanaged
        {
            return !Unsafe.IsAddressLessThan(ref left, ref right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressLessThanOrEqualTo<T>(ref T left, ref T right)
            where T : unmanaged
        {
            return !Unsafe.IsAddressGreaterThan(ref left, ref right);
        }
    }
}
#endif
