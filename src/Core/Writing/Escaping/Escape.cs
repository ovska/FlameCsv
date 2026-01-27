using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Writing.Escaping;

internal static class Escape
{
    /// <summary>
    /// Escapes <paramref name="source"/> into <paramref name="destination"/> by wrapping it in quotes and escaping
    /// possible escapes/quotes in the value.
    /// </summary>
    /// <param name="quoteArg">Quote character</param>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer. Can be the same memory region as the source</param>
    /// <param name="quoteCount">Number of quotes in the source</param>
    public static void Scalar<T>(T quoteArg, scoped ReadOnlySpan<T> source, scoped Span<T> destination, int quoteCount)
        where T : unmanaged, IBinaryInteger<T>
    {
        Check.GreaterThanOrEqual(destination.Length, source.Length + quoteCount + 2);
        Check.True(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            $"If src and dst overlap, they must have the same starting point in memory (was {elementOffset})"
        );

        T quote = quoteArg; // ensure loaded into register

        // Work backwards as the source and destination buffers might overlap
        nint srcRemaining = source.Length - 1;
        nint dstRemaining = source.Length + quoteCount + 1;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);

        Unsafe.Add(ref dst, dstRemaining) = quote;
        dstRemaining--;

        if (quoteCount == 0)
            goto End;

        // we only do one LastIndexOf call, assuming that most fields have at most one quote we can just do two memcpys
        int lastIndex = source.LastIndexOf(quote);

        // if this condition isn't true, quote was either not found, or it was the last token
        if ((uint)lastIndex < srcRemaining)
        {
            nint nonSpecialCount = srcRemaining - lastIndex + 1;
            Copy(ref src, (uint)lastIndex, ref dst, (nuint)(dstRemaining - nonSpecialCount + 1), (uint)nonSpecialCount);

            srcRemaining -= nonSpecialCount;
            dstRemaining -= nonSpecialCount;
            Unsafe.Add(ref dst, dstRemaining) = quote;
            dstRemaining--;

            if (--quoteCount == 0)
                goto End;
        }

        // read backwards until we find the last quote
        while (srcRemaining >= 0)
        {
            if (quote == Unsafe.Add(ref src, srcRemaining))
            {
                Unsafe.Add(ref dst, dstRemaining) = Unsafe.Add(ref src, srcRemaining);
                Unsafe.Add(ref dst, dstRemaining - 1) = quote;

                srcRemaining -= 1;
                dstRemaining -= 2;

                if (--quoteCount == 0)
                {
                    goto End;
                }
            }
            else
            {
                Unsafe.Add(ref dst, dstRemaining) = Unsafe.Add(ref src, srcRemaining);
                srcRemaining--;
                dstRemaining--;
            }
        }

        End:
        Copy(ref src, 0, ref dst, 1, (uint)srcRemaining + 1u);

        // the final quote must!! be written last since src and dst might occupy the same memory region
        dst = quote;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, nuint srcIndex, ref T dst, nuint dstIndex, uint length)
        {
            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length
            );
        }
    }
}
