using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Writing.Escaping;

internal static partial class Escape
{
    /// <summary>
    /// Escapes <paramref name="source"/> into <paramref name="destination"/> by wrapping it in quotes and escaping
    /// possible escapes/quotes in the value.
    /// </summary>
    /// <param name="escaper"></param>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer. Can be the same memory region as the source</param>
    /// <param name="specialCount">Number of quotes/escapes in the source</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Scalar<T, TEscaper>(
        TEscaper escaper,
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        int specialCount
    )
        where T : unmanaged, IBinaryInteger<T>
        where TEscaper : struct, IEscaper<T>
    {
        Debug.Assert(destination.Length >= source.Length + specialCount + 2, "Destination buffer is too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory"
        );

        // Work backwards as the source and destination buffers might overlap
        nint srcRemaining = source.Length - 1;
        nint dstRemaining = destination.Length - 1;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);

        Unsafe.Add(ref dst, dstRemaining) = escaper.Quote;
        dstRemaining--;

        if (specialCount == 0)
            goto End;

        int lastIndex = escaper.LastIndexOfEscapable(source);

        // either not found, or it was the last token
        if ((uint)lastIndex < srcRemaining)
        {
            nint nonSpecialCount = srcRemaining - lastIndex + 1;
            Copy(ref src, (uint)lastIndex, ref dst, (nuint)(dstRemaining - nonSpecialCount + 1), (uint)nonSpecialCount);

            srcRemaining -= nonSpecialCount;
            dstRemaining -= nonSpecialCount;
            Unsafe.Add(ref dst, dstRemaining) = escaper.Escape;
            dstRemaining--;

            if (--specialCount == 0)
                goto End;
        }

        while (srcRemaining >= 0)
        {
            if (escaper.NeedsEscaping(Unsafe.Add(ref src, srcRemaining)))
            {
                Unsafe.Add(ref dst, dstRemaining) = Unsafe.Add(ref src, srcRemaining);
                Unsafe.Add(ref dst, dstRemaining - 1) = escaper.Escape;

                srcRemaining -= 1;
                dstRemaining -= 2;

                if (--specialCount == 0)
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
        dst = escaper.Quote;

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
