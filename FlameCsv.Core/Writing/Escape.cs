using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Writing;

internal static class Escape
{
    /// <summary>
    /// Escapes <paramref name="source"/> into <paramref name="destination"/> by wrapping it in quotes and escaping
    /// possible quotes in the value.
    /// </summary>
    /// <param name="escaper"></param>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer, can be the same memory region as source</param>
    /// <param name="specialCount">Amount of quotes/escapes in the source</param>
    public static void Field<T, TEscaper>(
        ref TEscaper escaper,
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        int specialCount)
        where T : unmanaged, IBinaryInteger<T>
        where TEscaper : IEscaper<T>, allows ref struct
    {
        Debug.Assert(destination.Length >= source.Length + specialCount + 2, "Destination buffer is too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

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

            Copy(
                ref src,
                (nuint)lastIndex,
                ref dst,
                (nuint)(dstRemaining - nonSpecialCount + 1),
                (uint)nonSpecialCount);

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
                byteCount: (uint)Unsafe.SizeOf<T>() * length / sizeof(byte));
        }
    }

    public static void FieldWithOverflow<T, TEscaper>(
        ref TEscaper escaper,
        IBufferWriter<T> writer,
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        int specialCount,
        MemoryPool<T> allocator)
        where T : unmanaged, IBinaryInteger<T>
        where TEscaper : IEscaper<T>, allows ref struct
    {
        Debug.Assert(
            destination.Length < source.Length + specialCount + 2,
            "Destination buffer is too big, use regular escape!");

        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        int requiredLength = source.Length + specialCount + 2;

        // First write the overflowing data to the array, working backwards as source and destination
        // share a memory region
        int srcIndex = source.Length - 1;
        int ovrIndex = requiredLength - destination.Length - 1;
        int dstIndex = destination.Length - 1;
        bool needEscape = false;

        int overflowLength = requiredLength - destination.Length;
        scoped Span<T> overflow;
        IMemoryOwner<T>? allocated = null;

        if (Token<T>.CanStackalloc(overflowLength))
        {
            overflow = stackalloc T[overflowLength];
        }
        else
        {
            allocated = allocator.Rent(overflowLength);
            overflow = allocated.Memory.Span.Slice(0, overflowLength);
        }

        overflow[ovrIndex--] = escaper.Quote; // write closing quote

        // Short circuit to faster impl if there are no quotes in the source data
        if (specialCount == 0)
            goto CopyTo;

        // Copy tokens one-at-a-time until all quotes have been escaped and use the faster impl after
        while (ovrIndex >= 0)
        {
            if (needEscape)
            {
                overflow[ovrIndex--] = escaper.Escape;
                needEscape = false;

                if (--specialCount == 0)
                    goto CopyTo;
            }
            else if (escaper.NeedsEscaping(source[srcIndex]))
            {
                overflow[ovrIndex--] = source[srcIndex--];
                needEscape = true;
            }
            else
            {
                overflow[ovrIndex--] = source[srcIndex--];
            }
        }

        while (srcIndex >= 0)
        {
            if (needEscape)
            {
                destination[dstIndex--] = escaper.Escape;
                needEscape = false;

                if (--specialCount == 0)
                    goto CopyTo;
            }
            else if (escaper.NeedsEscaping(source[srcIndex]))
            {
                destination[dstIndex--] = source[srcIndex--];
                needEscape = true;
            }
            else
            {
                destination[dstIndex--] = source[srcIndex--];
            }
        }

        // true if the first token in the source is a quote
        if (needEscape)
        {
            destination[dstIndex--] = escaper.Escape;
            specialCount--;
        }

    CopyTo:
        if (srcIndex > 0)
        {
            if (ovrIndex >= 0)
            {
                source.Slice(srcIndex, ovrIndex + 1).CopyTo(overflow);
                srcIndex -= ovrIndex + 1;
            }

            // dst needs 1 slot for the opening quote
            if (dstIndex > 1)
            {
                source.Slice(0, srcIndex + 1).CopyTo(destination.Slice(1));
                dstIndex = 0;
            }
        }

        destination[dstIndex] = escaper.Quote; // write opening quote

        Debug.Assert(dstIndex == 0);
        Debug.Assert(specialCount == 0);

        // the whole of the destination is filled, with the leftovers being written to the overflow
        writer.Advance(destination.Length);

        // copy leftovers and advance
        overflow.CopyTo(writer.GetSpan(overflow.Length));
        writer.Advance(overflow.Length);

        allocated?.Dispose();
    }
}
