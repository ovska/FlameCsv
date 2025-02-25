using CommunityToolkit.HighPerformance;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

[ExcludeFromCodeCoverage]
internal static class RFC4180Mode<T> where T : unmanaged, IBinaryInteger<T>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Unescape(
        T quote,
        scoped Span<T> buffer,
        ReadOnlySpan<T> field,
        uint quotesConsumed)
    {
        Debug.Assert(quotesConsumed >= 2);
        Debug.Assert(quotesConsumed % 2 == 0);

        uint quotesLeft = quotesConsumed;

        nuint srcIndex = 0;
        nuint srcLength = (nuint)field.Length;
        nuint dstIndex = 0;

        ref T src = ref field.DangerousGetReference();
        ref T dst = ref buffer.DangerousGetReference();

        goto ContinueRead;

    Found1:
        Copy(ref src, srcIndex, ref dst, dstIndex, 1);
        srcIndex += 1;
        dstIndex += 1;
        goto FoundLong;
    Found2:
        Copy(ref src, srcIndex, ref dst, dstIndex, 2);
        srcIndex += 2;
        dstIndex += 2;
        goto FoundLong;
    Found3:
        Copy(ref src, srcIndex, ref dst, dstIndex, 3);
        srcIndex += 3;
        dstIndex += 3;
        goto FoundLong;
    Found4:
        Copy(ref src, srcIndex, ref dst, dstIndex, 4);
        srcIndex += 4;
        dstIndex += 4;
        goto FoundLong;
    Found5:
        Copy(ref src, srcIndex, ref dst, dstIndex, 5);
        srcIndex += 5;
        dstIndex += 5;
        goto FoundLong;
    Found6:
        Copy(ref src, srcIndex, ref dst, dstIndex, 6);
        srcIndex += 6;
        dstIndex += 6;
        goto FoundLong;
    Found7:
        Copy(ref src, srcIndex, ref dst, dstIndex, 7);
        srcIndex += 7;
        dstIndex += 7;
        goto FoundLong;
    Found8:
        Copy(ref src, srcIndex, ref dst, dstIndex, 8);
        srcIndex += 8;
        dstIndex += 8;

    FoundLong:
        if (srcIndex >= srcLength || quote != Unsafe.Add(ref src, srcIndex))
            ThrowInvalidUnescape(field, quote, quotesConsumed);

        srcIndex++;

        quotesLeft -= 2;

        if (quotesLeft == 0)
            goto NoQuotesLeft;

    ContinueRead:
        // TODO: use ISimdVector
        if (Vector128.IsHardwareAccelerated &&
            Vector128<T>.IsSupported &&
            srcLength - srcIndex >= (nuint)Vector128<T>.Count)
        {
            Vector128<T> quoteVector = Vector128.Create(quote);

            do
            {
                Vector128<T> current = Vector128.LoadUnsafe(ref Unsafe.Add(ref src, srcIndex));
                Vector128<T> equals = Vector128.Equals(current, quoteVector);

                if (equals == Vector128<T>.Zero)
                {
                    Copy(ref src, srcIndex, ref dst, dstIndex, (uint)Vector128<T>.Count);
                    srcIndex += (nuint)Vector128<T>.Count;
                    dstIndex += (nuint)Vector128<T>.Count;
                    continue;
                }

                uint mask = equals.ExtractMostSignificantBits();
                uint charpos = uint.TrailingZeroCount(mask) + 1;

                Copy(ref src, srcIndex, ref dst, dstIndex, charpos);
                srcIndex += charpos;
                dstIndex += charpos;
                goto FoundLong;
            } while (srcLength - srcIndex >= (nuint)Vector128<T>.Count);
        }

        while (srcLength - srcIndex >= 8)
        {
            if (quote == Unsafe.Add(ref src, srcIndex + 0))
                goto Found1;

            if (quote == Unsafe.Add(ref src, srcIndex + 1))
                goto Found2;

            if (quote == Unsafe.Add(ref src, srcIndex + 2))
                goto Found3;

            if (quote == Unsafe.Add(ref src, srcIndex + 3))
                goto Found4;

            if (quote == Unsafe.Add(ref src, srcIndex + 4))
                goto Found5;

            if (quote == Unsafe.Add(ref src, srcIndex + 5))
                goto Found6;

            if (quote == Unsafe.Add(ref src, srcIndex + 6))
                goto Found7;

            if (quote == Unsafe.Add(ref src, srcIndex + 7))
                goto Found8;

            srcIndex += 8;
            dstIndex += 8;
        }

        while (srcIndex < srcLength)
        {
            if (quote == Unsafe.Add(ref src, srcIndex))
            {
                srcIndex++;

                if (srcIndex >= srcLength || quote != Unsafe.Add(ref src, srcIndex))
                    ThrowInvalidUnescape(field, quote, quotesConsumed);

                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;

                quotesLeft -= 2;

                if (quotesLeft == 0)
                    goto NoQuotesLeft;
            }
            else
            {
                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;
            }
        }

        goto EOL;

        // Copy remaining data
    NoQuotesLeft:
        Copy(ref src, srcIndex, ref dst, dstIndex, (uint)(srcLength - srcIndex));

    EOL:
        if (quotesLeft != 0)
            ThrowInvalidUnescape(field, quote, quotesConsumed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, nuint srcIndex, ref T dst, nuint dstIndex, uint length)
        {
            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length);
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidUnescape(
        ReadOnlySpan<T> field,
        T quote,
        uint quoteCount)
    {
        int actualCount = System.MemoryExtensions.Count(field, quote);

        var error = new StringBuilder(64);

        if (field.Length < 2)
        {
            error.Append(CultureInfo.InvariantCulture, $"Source is too short (length: {field.Length}). ");
        }

        if (actualCount != quoteCount)
        {
            error.Append(
                CultureInfo.InvariantCulture,
                $"String delimiter count {quoteCount} was invalid (actual was {actualCount}). ");
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in field)
        {
            error.Append(token.Equals(quote) ? '"' : 'x');
        }

        error.Append(']');

        throw new UnreachableException($"Internal error, failed to unescape (token: {typeof(T).FullName}): {error}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Unescape<TVector>(
        T quote,
        scoped Span<T> buffer,
        ref readonly T first,
        int length,
        uint quotesConsumed)
        where TVector : struct, ISimdVector<T, TVector>
    {
        Unescape<TVector>(quote, buffer, MemoryMarshal.CreateReadOnlySpan(in first, length), quotesConsumed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Unescape<TVector>(
        T quote,
        scoped Span<T> buffer,
        ReadOnlySpan<T> field,
        uint quotesConsumed)
        where TVector : struct, ISimdVector<T, TVector>
    {
        Debug.Assert(quotesConsumed >= 2);
        Debug.Assert(quotesConsumed % 2 == 0);

        uint quotesLeft = quotesConsumed;

        nuint srcIndex = 0;
        nuint srcLength = (nuint)field.Length;
        nuint dstIndex = 0;

        ref T src = ref field.DangerousGetReference();
        ref T dst = ref buffer.DangerousGetReference();

        goto ContinueRead;

    Found1:
        Copy(ref src, srcIndex, ref dst, dstIndex, 1);
        srcIndex += 1;
        dstIndex += 1;
        goto FoundLong;
    Found2:
        Copy(ref src, srcIndex, ref dst, dstIndex, 2);
        srcIndex += 2;
        dstIndex += 2;
        goto FoundLong;
    Found3:
        Copy(ref src, srcIndex, ref dst, dstIndex, 3);
        srcIndex += 3;
        dstIndex += 3;
        goto FoundLong;
    Found4:
        Copy(ref src, srcIndex, ref dst, dstIndex, 4);
        srcIndex += 4;
        dstIndex += 4;
        goto FoundLong;
    Found5:
        Copy(ref src, srcIndex, ref dst, dstIndex, 5);
        srcIndex += 5;
        dstIndex += 5;
        goto FoundLong;
    Found6:
        Copy(ref src, srcIndex, ref dst, dstIndex, 6);
        srcIndex += 6;
        dstIndex += 6;
        goto FoundLong;
    Found7:
        Copy(ref src, srcIndex, ref dst, dstIndex, 7);
        srcIndex += 7;
        dstIndex += 7;
        goto FoundLong;
    Found8:
        Copy(ref src, srcIndex, ref dst, dstIndex, 8);
        srcIndex += 8;
        dstIndex += 8;

    FoundLong:
        if (srcIndex >= srcLength || quote != Unsafe.Add(ref src, srcIndex))
            ThrowInvalidUnescape(field, quote, quotesConsumed);

        srcIndex++;

        quotesLeft -= 2;

        if (quotesLeft == 0)
            goto NoQuotesLeft;

    ContinueRead:
        if (TVector.IsSupported && srcLength - srcIndex >= (nuint)TVector.Count)
        {
            TVector quoteVector = TVector.Create(quote);

            do
            {
                TVector current = TVector.LoadUnsafe(ref src, srcIndex);
                TVector equals = TVector.Equals(current, quoteVector);

                if (equals == TVector.Zero)
                {
                    Copy(ref src, srcIndex, ref dst, dstIndex, (uint)TVector.Count);
                    srcIndex += (nuint)TVector.Count;
                    dstIndex += (nuint)TVector.Count;
                    continue;
                }

                nuint mask = equals.ExtractMostSignificantBits();
                uint charpos = (uint)BitOperations.TrailingZeroCount(mask) + 1;

                Copy(ref src, srcIndex, ref dst, dstIndex, charpos);
                srcIndex += charpos;
                dstIndex += charpos;
                goto FoundLong;
            } while (srcLength - srcIndex >= (nuint)TVector.Count);
        }

        while (srcLength - srcIndex >= 8)
        {
            if (quote == Unsafe.Add(ref src, srcIndex + 0))
                goto Found1;

            if (quote == Unsafe.Add(ref src, srcIndex + 1))
                goto Found2;

            if (quote == Unsafe.Add(ref src, srcIndex + 2))
                goto Found3;

            if (quote == Unsafe.Add(ref src, srcIndex + 3))
                goto Found4;

            if (quote == Unsafe.Add(ref src, srcIndex + 4))
                goto Found5;

            if (quote == Unsafe.Add(ref src, srcIndex + 5))
                goto Found6;

            if (quote == Unsafe.Add(ref src, srcIndex + 6))
                goto Found7;

            if (quote == Unsafe.Add(ref src, srcIndex + 7))
                goto Found8;

            srcIndex += 8;
            dstIndex += 8;
        }

        while (srcIndex < srcLength)
        {
            if (quote == Unsafe.Add(ref src, srcIndex))
            {
                srcIndex++;

                if (srcIndex >= srcLength || quote != Unsafe.Add(ref src, srcIndex))
                    ThrowInvalidUnescape(field, quote, quotesConsumed);

                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;

                quotesLeft -= 2;

                if (quotesLeft == 0)
                    goto NoQuotesLeft;
            }
            else
            {
                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;
            }
        }

        goto EOL;

        // Copy remaining data
    NoQuotesLeft:
        Copy(ref src, srcIndex, ref dst, dstIndex, (uint)(srcLength - srcIndex));

    EOL:
        if (quotesLeft != 0)
            ThrowInvalidUnescape(field, quote, quotesConsumed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, nuint srcIndex, ref T dst, nuint dstIndex, uint length)
        {
            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length);
        }
    }
}
