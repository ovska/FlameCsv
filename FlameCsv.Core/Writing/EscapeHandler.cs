using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Writing;

internal static class EscapeHandler
{
    /// <summary>
    /// Number of bits in a single mask.
    /// </summary>
    public const int MaskSize = sizeof(uint) * 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int firstLength, int padding) GetPadding(int length, int maskSize)
    {
        int remainder = length & (maskSize - 1);
        int firstLength = remainder | ((remainder - 1) >> 31 & maskSize);
        int padding = maskSize - firstLength;
        return (firstLength, padding);
    }

    /// <summary>
    /// Gets a span of unsigned integers to be used as a bit buffer, either from stack memory or array pool.
    /// </summary>
    /// <param name="valueLength">Value length.</param>
    /// <param name="buffer">A stack-allocated buffer to use if large enough.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<uint> GetBitBuffer(int valueLength, Span<uint> buffer)
    {
        int requiredLength = (valueLength + MaskSize - 1) / MaskSize;

        if (buffer.Length >= requiredLength)
        {
            return buffer[..requiredLength];
        }

        // TODO: Use ArrayPool
        return GC.AllocateUninitializedArray<uint>(requiredLength);
    }

    /// <summary>
    /// Determines if the value needs escaping.
    /// </summary>
    /// <param name="value">Value containing the field to check.</param>
    /// <param name="valueLength">Length of the field</param>
    /// <param name="bitbuffer">A buffer to store the positions of quote characters.</param>
    /// <param name="delimiterArg">The delimiter.</param>
    /// <param name="quoteArg">The quote.</param>
    /// <param name="newline">The newline detector instance.</param>
    /// <param name="quoteCount">The number of quote characters found in the value.</param>
    /// <returns><c>true</c> if the value needs escaping (contains delimiters, quotes, or newlines); otherwise, <c>false</c>.</returns>
    public static bool NeedsEscaping<T, TNewline, TVector>(
        ReadOnlySpan<T> value,
        int valueLength,
        Span<uint> bitbuffer,
        T delimiterArg,
        T quoteArg,
        scoped ref readonly TNewline newline,
        out int quoteCount)
        where T : unmanaged, IBinaryInteger<T>
        where TNewline : INewline<T, TVector>
        where TVector : struct, ISimdVector<T, TVector>
    {
        Debug.Assert(valueLength >= value.Length, "Value length should be at least the length of the value.");
        Debug.Assert(value.Length >= TVector.Count, "NeedsEscaping needs a value at least one vector's length.");
        Debug.Assert(bitbuffer.Length >= (value.Length + MaskSize - 1) / MaskSize, "Bitbuffer is too small for the value length.");
        Debug.Assert(TVector.Count == MaskSize, "TVector.Count should be 32 for this implementation.");

        quoteCount = 0;
        TVector delimiter = TVector.Create(delimiterArg);
        TVector quote = TVector.Create(quoteArg);
        TVector needsEscaping = TVector.Zero;

        ref T first = ref MemoryMarshal.GetReference(value);
        ref uint bitRef = ref MemoryMarshal.GetReference(bitbuffer);
        nuint offset = 0;
        uint bitOffset = 0;
        nint remaining = valueLength;

        // Handle the first (potentially partial) vector
        (int firstLength, int padding) = GetPadding(value.Length, TVector.Count);

        quoteCount += HandleBlock(
            ref first,
            ref bitRef,
            offset,
            bitOffset,
            padding,
            in newline,
            in quote,
            in delimiter,
            ref needsEscaping);

        offset += (nuint)firstLength;
        remaining -= firstLength;
        bitOffset++;

        // Process remaining full vectors
        while (remaining > 0)
        {
            quoteCount += HandleBlock(
                ref first,
                ref bitRef,
                offset,
                bitOffset,
                padding: 0,
                in newline,
                in quote,
                in delimiter,
                ref needsEscaping);

            offset += (nuint)TVector.Count;
            remaining -= TVector.Count;
            bitOffset++;
        }

        return needsEscaping != TVector.Zero;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HandleBlock(
            scoped ref T first,
            scoped ref uint bitRef,
            nuint offset,
            nuint bitOffset,
            int padding,
            scoped ref readonly TNewline newline,
            scoped ref readonly TVector quote,
            scoped ref readonly TVector delimiter,
            scoped ref TVector needsEscaping)
        {
            TVector current = TVector.LoadUnaligned(ref first, offset);
            TVector hasQuote = TVector.Equals(current, quote);
            TVector hasDelimiter = TVector.Equals(current, delimiter);
            TVector hasNewline = newline.HasNewline(current);

            needsEscaping |= (hasQuote | hasDelimiter | hasNewline);

            uint mask = (uint)hasQuote.ExtractMostSignificantBits();

            // For partial vectors (padding != 0), shift right to align valid bits at LSB so LZCNT can be used
            // JIT should get rid of this in the loop
            if (padding != 0)
            {
                mask >>= padding;
            }

            Unsafe.Add(ref bitRef, bitOffset) = mask;
            return BitOperations.PopCount(mask);
        }
    }

    public static void Escape<T>(
        ReadOnlySpan<T> source,
        Span<T> destination,
        Span<uint> bitbuffer,
        T quote)
        where T : unmanaged, IBinaryInteger<T>
    {
        Debug.Assert(destination.Length > source.Length, "Destination buffer must be larger than source.");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "Source and destination buffers must not overlap, except if they start at the same region.");

        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nint srcRemaining = source.Length;
        nint dstRemaining = destination.Length;

        ref uint firstMask = ref MemoryMarshal.GetReference(bitbuffer);
        nint masksRemaining = bitbuffer.Length - 1;

        // read all but the last mask
        while (masksRemaining > 0)
        {
            ProcessMask(
                quote,
                Unsafe.Add(ref firstMask, masksRemaining),
                0,
                ref src,
                ref srcRemaining,
                ref dst,
                ref dstRemaining);

            masksRemaining--;
        }

        // read the last mask
        ProcessMask(
            quote,
            firstMask,
            maskConsumed: srcRemaining == MaskSize ? 0 : MaskSize - (int)srcRemaining,
            ref src,
            ref srcRemaining,
            ref dst,
            ref dstRemaining);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ProcessMask(
            T escape,
            uint mask,
            int maskConsumed,
            scoped ref T src,
            scoped ref nint srcRemaining,
            scoped ref T dst,
            scoped ref nint dstRemaining)
        {
            while (mask != 0)
            {
                int quotePos = BitOperations.LeadingZeroCount(mask) + 1;
                mask &= uint.MaxValue >> (quotePos); // clear the leading bit

                int quoteOffset = quotePos - maskConsumed;
                maskConsumed = quotePos; // store the previous quote position

                // copy the data between the quotes, including the quote
                srcRemaining -= quoteOffset;
                dstRemaining -= quoteOffset;
                Copy(ref src, srcRemaining, ref dst, dstRemaining, (uint)quoteOffset);
                Unsafe.Add(ref dst, --dstRemaining) = escape;
            }

            // copy the remaining data
            int maskRemaining = (sizeof(uint) * 8) - maskConsumed;
            srcRemaining -= maskRemaining;
            dstRemaining -= maskRemaining;
            Copy(ref src, srcRemaining, ref dst, dstRemaining, (uint)maskRemaining);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(scoped ref T src, nint srcIndex, scoped ref T dst, nint dstIndex, uint length)
        {
            Debug.Assert(srcIndex >= 0);
            Debug.Assert(dstIndex >= 0);
            Debug.Assert(length >= 0);

            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length);
        }
    }
}
