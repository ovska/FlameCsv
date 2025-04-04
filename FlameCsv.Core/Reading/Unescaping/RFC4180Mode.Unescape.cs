using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace FlameCsv.Reading.Unescaping;

[SkipLocalsInit]
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
        Debug.Assert(field.Length >= 2);
        Debug.Assert(field.Length >= quotesConsumed);

        int quotesLeft = (int)quotesConsumed;

        nint srcIndex = 0;
        nint dstIndex = 0;

        Vector256<T> quote256 = Vector256.IsHardwareAccelerated ? Vector256.Create(quote) : default;
        Vector128<T> quote128 = Vector128.IsHardwareAccelerated ? Vector128.Create(quote) : default;

        // leave 1 space for the second quote
        nint remaining = field.Length;

        ref T src = ref MemoryMarshal.GetReference(field);
        ref T dst = ref MemoryMarshal.GetReference(buffer);

        goto ContinueRead;

    Found1:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        srcIndex += 1;
        dstIndex += 1;
        remaining -= 1;
        goto FoundLong;
    Found2:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        srcIndex += 2;
        dstIndex += 2;
        remaining -= 2;
        goto FoundLong;
    Found3:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        Unsafe.Add(ref dst, dstIndex + 2) = Unsafe.Add(ref src, srcIndex + 2);
        srcIndex += 3;
        dstIndex += 3;
        remaining -= 3;
        goto FoundLong;
    Found4:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        Unsafe.Add(ref dst, dstIndex + 2) = Unsafe.Add(ref src, srcIndex + 2);
        Unsafe.Add(ref dst, dstIndex + 3) = Unsafe.Add(ref src, srcIndex + 3);
        srcIndex += 4;
        dstIndex += 4;
        remaining -= 4;
        goto FoundLong;
    Found5:
        Copy(ref src, srcIndex, ref dst, dstIndex, 5);
        srcIndex += 5;
        dstIndex += 5;
        remaining -= 5;
        goto FoundLong;
    Found6:
        Copy(ref src, srcIndex, ref dst, dstIndex, 6);
        srcIndex += 6;
        dstIndex += 6;
        remaining -= 6;
        goto FoundLong;
    Found7:
        Copy(ref src, srcIndex, ref dst, dstIndex, 7);
        srcIndex += 7;
        dstIndex += 7;
        remaining -= 7;
        goto FoundLong;
    Found8:
        Copy(ref src, srcIndex, ref dst, dstIndex, 8);
        srcIndex += 8;
        dstIndex += 8;
        remaining -= 8;

    FoundLong:
        if (quote != Unsafe.Add(ref src, srcIndex)) goto Fail;

        srcIndex++;
        remaining--;

        quotesLeft -= 2;

        if (quotesLeft <= 0) goto NoQuotesLeft;

    ContinueRead:
        while (
            Vector256.IsHardwareAccelerated &&
            remaining >= Vector256<T>.Count)
        {
            var current = Vector256.LoadUnsafe(ref src, (nuint)srcIndex);
            var equals = Vector256.Equals(current, quote256);
            current.StoreUnsafe(ref dst, (nuint)dstIndex);

            if (equals == Vector256<T>.Zero)
            {
                srcIndex += Vector256<T>.Count;
                dstIndex += Vector256<T>.Count;
                remaining -= Vector256<T>.Count;
                continue;
            }

            uint mask = equals.ExtractMostSignificantBits();
            int charpos = BitOperations.TrailingZeroCount(mask) + 1;
            srcIndex += charpos;
            dstIndex += charpos;
            remaining -= charpos;
            goto FoundLong;
        }

        // 128bit vector is only 8 chars,
        // it's slightly faster to defer to the unrolled loop
        while (
            Unsafe.SizeOf<T>() == sizeof(byte) &&
            Vector128.IsHardwareAccelerated &&
            remaining >= Vector128<T>.Count)
        {
            var current = Vector128.LoadUnsafe(ref src, (nuint)srcIndex);
            var equals = Vector128.Equals(current, quote128);
            current.StoreUnsafe(ref dst, (nuint)dstIndex);

            if (equals == Vector128<T>.Zero)
            {
                srcIndex += Vector128<T>.Count;
                dstIndex += Vector128<T>.Count;
                remaining -= Vector128<T>.Count;
                continue;
            }

            uint mask = equals.ExtractMostSignificantBits();
            int charpos = BitOperations.TrailingZeroCount(mask) + 1;
            srcIndex += charpos;
            dstIndex += charpos;
            remaining -= charpos;
            goto FoundLong;
        }

        while (remaining >= 8)
        {
            if (quote == Unsafe.Add(ref src, srcIndex + 0)) goto Found1;
            if (quote == Unsafe.Add(ref src, srcIndex + 1)) goto Found2;
            if (quote == Unsafe.Add(ref src, srcIndex + 2)) goto Found3;
            if (quote == Unsafe.Add(ref src, srcIndex + 3)) goto Found4;
            if (quote == Unsafe.Add(ref src, srcIndex + 4)) goto Found5;
            if (quote == Unsafe.Add(ref src, srcIndex + 5)) goto Found6;
            if (quote == Unsafe.Add(ref src, srcIndex + 6)) goto Found7;
            if (quote == Unsafe.Add(ref src, srcIndex + 7)) goto Found8;

            Copy(ref src, srcIndex, ref dst, dstIndex, 8);
            srcIndex += 8;
            dstIndex += 8;
            remaining -= 8;
        }

        while (remaining >= 0)
        {
            if (quote == Unsafe.Add(ref src, srcIndex))
            {
                if (quote != Unsafe.Add(ref src, ++srcIndex)) goto Fail;

                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;
                remaining--;

                quotesLeft -= 2;

                if (quotesLeft <= 0) goto NoQuotesLeft;
            }
            else
            {
                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;
                remaining--;
            }
        }

        goto EOL;

        // Copy remaining data
    NoQuotesLeft:
        Copy(ref src, srcIndex, ref dst, dstIndex, (uint)(remaining));

    EOL:
        if (quotesLeft != 0)
        {
            ThrowInvalidUnescape(field, quote, quotesConsumed);
        }

        return;

    Fail:
        ThrowInvalidUnescape(field, quote, quotesConsumed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, nint srcIndex, ref T dst, nint dstIndex, uint length)
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
}
