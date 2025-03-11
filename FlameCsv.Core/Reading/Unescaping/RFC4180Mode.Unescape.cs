using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading.Unescaping;

[SkipLocalsInit]
internal static class RFC4180Mode<T> where T : unmanaged, IBinaryInteger<T>
{
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
        Debug.Assert(field.Length >= 2);
        Debug.Assert(field.Length >= quotesConsumed);

        int quotesLeft = (int)quotesConsumed;

        nint srcIndex = 0;
        nint dstIndex = 0;
        nint srcLength = field.Length;

        // leave 1 space for the second quote
        nint searchSpaceEnd = field.Length - 1;
        nint unrolledEnd = field.Length - 8 - 1;
        nint vectorizedEnd = field.Length - 1 - TVector.Count;

        ref T src = ref MemoryMarshal.GetReference(field);
        ref T dst = ref MemoryMarshal.GetReference(buffer);

        TVector quoteVector = TVector.Create(quote);

        goto ContinueRead;

    Found1:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        srcIndex += 1;
        dstIndex += 1;
        goto FoundLong;
    Found2:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        srcIndex += 2;
        dstIndex += 2;
        goto FoundLong;
    Found3:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        Unsafe.Add(ref dst, dstIndex + 2) = Unsafe.Add(ref src, srcIndex + 2);
        srcIndex += 3;
        dstIndex += 3;
        goto FoundLong;
    Found4:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        Unsafe.Add(ref dst, dstIndex + 2) = Unsafe.Add(ref src, srcIndex + 2);
        Unsafe.Add(ref dst, dstIndex + 3) = Unsafe.Add(ref src, srcIndex + 3);
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
        if (quote != Unsafe.Add(ref src, srcIndex)) goto Fail;

        srcIndex++;

        quotesLeft -= 2;

        if (quotesLeft <= 0) goto NoQuotesLeft;

    ContinueRead:
        while (TVector.IsSupported && srcIndex < vectorizedEnd)
        {
            TVector current = TVector.LoadUnaligned(ref src, (nuint)srcIndex);
            TVector equals = TVector.Equals(current, quoteVector);

            if (equals == TVector.Zero)
            {
                Copy(ref src, srcIndex, ref dst, dstIndex, (uint)TVector.Count);
                srcIndex += TVector.Count;
                dstIndex += TVector.Count;
                continue;
            }

            nuint mask = equals.ExtractMostSignificantBits();
            int charpos = BitOperations.TrailingZeroCount(mask) + 1;

            Copy(ref src, srcIndex, ref dst, dstIndex, (uint)charpos);
            srcIndex += charpos;
            dstIndex += charpos;
            goto FoundLong;
        }

        while (srcIndex < unrolledEnd)
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
        }

        while (srcIndex < searchSpaceEnd)
        {
            if (quote == Unsafe.Add(ref src, srcIndex))
            {
                if (quote != Unsafe.Add(ref src, ++srcIndex)) goto Fail;

                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;

                quotesLeft -= 2;

                if (quotesLeft <= 0) goto NoQuotesLeft;
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
