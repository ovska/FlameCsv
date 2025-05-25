using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Intrinsics;

namespace FlameCsv.Writing.Escaping;

internal static partial class Escape
{
    /// <summary>
    /// Number of bits in a single mask.
    /// </summary>
    public const int MaskSize = sizeof(uint) * 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int firstLength, int padding) GetPadding(int length, int maskSize)
    {
        int remainder = length & (maskSize - 1);
        int firstLength = remainder | ((remainder - 1) >> (MaskSize - 1) & maskSize);
        int padding = maskSize - firstLength;
        return (firstLength, padding);
    }

    /// <summary>
    /// Gets a span of unsigned integers to be used as a bit buffer, either from stack memory or array pool.
    /// </summary>
    /// <param name="valueLength">Value length.</param>
    /// <param name="buffer">A stack-allocated buffer to use if large enough.</param>
    /// <param name="arrayPoolBuffer">A buffer from the array pool to use if the stack buffer is too small.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<uint> GetMaskBuffer(int valueLength, Span<uint> buffer, ref uint[]? arrayPoolBuffer)
    {
        int requiredLength = (valueLength + MaskSize - 1) / MaskSize;

        if (buffer.Length >= requiredLength)
        {
            return buffer[..requiredLength];
        }

        if (arrayPoolBuffer == null || arrayPoolBuffer.Length < requiredLength)
        {
            arrayPoolBuffer = ArrayPool<uint>.Shared.Rent(requiredLength);
        }

        return arrayPoolBuffer.AsSpan(0, requiredLength);
    }

    /// <summary>
    /// Determines if the value needs escaping.
    /// </summary>
    /// <param name="value">Value containing the field to check.</param>
    /// <param name="masks">A buffer to store the positions of quote characters.</param>
    /// <param name="tokens"></param>
    /// <param name="quoteCount">The number of quote characters found in the value.</param>
    /// <returns><c>true</c> if the value needs escaping (contains delimiters, quotes, or newlines); otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRequired<T, TTokens, TVector>(
        ReadOnlySpan<T> value,
        Span<uint> masks,
        scoped ref readonly TTokens tokens,
        out int quoteCount
    )
        where T : unmanaged, IBinaryInteger<T>
        where TTokens : struct, ISimdEscaper<T, TVector>
        where TVector : struct, IAsciiVector<TVector>
    {
        Debug.Assert(value.Length >= TVector.Count, "NeedsEscaping needs a value at least one vector's length.");
        Debug.Assert(
            masks.Length >= (value.Length + MaskSize - 1) / MaskSize,
            "Bitbuffer is too small for the value length."
        );
        Debug.Assert(TVector.Count == MaskSize, "TVector.Count should be 32 for this implementation.");

        quoteCount = 0;
        TVector needsQuoting = TVector.Zero;

        ref T first = ref MemoryMarshal.GetReference(value);
        ref uint maskDst = ref MemoryMarshal.GetReference(masks);
        nuint offset = 0;
        uint bitOffset = 0;
        nint remaining = value.Length;

        // Handle the first (potentially partial) vector
        (int firstLength, int padding) = GetPadding(value.Length, TVector.Count);

        TVector current = TVector.LoadUnaligned(ref first, offset);
        uint mask = tokens.FindEscapable(in current, ref needsQuoting);

        // For partial vectors (padding != 0), shift right to align valid bits at LSB so LZCNT can be used
        if (padding != 0)
        {
            mask <<= padding;
        }

        Unsafe.Add(ref maskDst, bitOffset) = mask;
        quoteCount += BitOperations.PopCount(mask);

        offset += (nuint)firstLength;
        remaining -= firstLength;
        bitOffset++;

        // Process remaining full vectors
        while (remaining > 0)
        {
            current = TVector.LoadUnaligned(ref first, offset);
            mask = tokens.FindEscapable(in current, ref needsQuoting);
            Unsafe.Add(ref maskDst, bitOffset) = mask;
            quoteCount += BitOperations.PopCount(mask);
            offset += (nuint)TVector.Count;
            remaining -= TVector.Count;
            bitOffset++;
        }

        return needsQuoting != TVector.Zero;
    }

    /// <summary>
    /// Escapes the source value into the destination buffer, inserting <paramref name="escape"/> characters
    /// before set bits in the <paramref name="masks"/>.
    /// </summary>
    /// <param name="source">Value to escape</param>
    /// <param name="destination">Destination buffer to escape to; must be exactly the required length</param>
    /// <param name="masks">Buffer containing masks for characters that need escaping</param>
    /// <param name="escape">Escape character to write before quotes/escapes in the source</param>
    /// <remarks>
    /// Does not write the wrapping quotes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FromMasks<T>(ReadOnlySpan<T> source, Span<T> destination, Span<uint> masks, T escape)
        where T : unmanaged, IBinaryInteger<T>
    {
        Debug.Assert(destination.Length > source.Length, "Destination buffer must be larger than source.");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "Source and destination buffers must not overlap, except if they start at the same region."
        );

        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nint srcRemaining = source.Length;
        nint dstRemaining = destination.Length;

        ref uint firstMask = ref MemoryMarshal.GetReference(masks);
        nint masksRemaining = masks.Length - 1;

        // read all but the last mask: copy the data in reverse order so the source and destination can share a buffer
        while (masksRemaining > 0)
        {
            ProcessMask(
                escape,
                Unsafe.Add(ref firstMask, masksRemaining),
                0,
                ref src,
                ref srcRemaining,
                ref dst,
                ref dstRemaining
            );

            masksRemaining--;
        }

        // read the last mask
        ProcessMask(
            escape,
            firstMask,
            maskConsumed: srcRemaining == MaskSize ? 0 : MaskSize - (int)srcRemaining,
            ref src,
            ref srcRemaining,
            ref dst,
            ref dstRemaining
        );

        Debug.Assert(srcRemaining == 0);
        Debug.Assert(dstRemaining == 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ProcessMask(
            T escape,
            uint mask,
            int maskConsumed,
            scoped ref T src,
            scoped ref nint srcRemaining,
            scoped ref T dst,
            scoped ref nint dstRemaining
        )
        {
            int previousQuotePosition = 0;

            while (mask != 0)
            {
                int quotePos = BitOperations.LeadingZeroCount(mask) + 1;
                mask &= (uint)((ulong)uint.MaxValue >> quotePos); // clear the leading bit

                int quoteOffset = quotePos - previousQuotePosition;
                previousQuotePosition = quotePos; // store the previous quote position

                // copy the data between the quotes, including the quote
                srcRemaining -= quoteOffset;
                dstRemaining -= quoteOffset;
                Copy(ref src, srcRemaining, ref dst, dstRemaining, (uint)quoteOffset);
                dstRemaining--;
                Unsafe.Add(ref dst, dstRemaining) = escape;
            }

            // copy the remaining data
            int maskRemaining = MaskSize - previousQuotePosition - maskConsumed;
            srcRemaining -= maskRemaining;
            dstRemaining -= maskRemaining;
            Copy(ref src, srcRemaining, ref dst, dstRemaining, (uint)maskRemaining);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(scoped ref T src, nint srcIndex, scoped ref T dst, nint dstIndex, uint length)
        {
            Debug.Assert(srcIndex >= 0);
            Debug.Assert(dstIndex >= 0);
            Debug.Assert(length < int.MaxValue);

            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length
            );
        }
    }
}
