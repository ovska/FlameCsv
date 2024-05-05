using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Writing;

internal interface IEscaper<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Escape character.
    /// </summary>
    T Escape { get; }

    /// <summary>
    /// String delimiter.
    /// </summary>
    T Quote { get; }

    /// <summary>
    /// Counts the number of special characters in the span.
    /// </summary>
    int CountEscapable(ReadOnlySpan<T> value);

    /// <inheritdoc cref="NeedsEscaping(T)"/>
    bool NeedsEscaping(ReadOnlySpan<T> value, out int specialCount);

    /// <summary>
    /// Returns <see langword="true"/> if the value contains any special characters that need to be escaped/quoted.
    /// </summary>
    bool NeedsEscaping(T value);

    /// <summary>
    /// Escapes <paramref name="source"/> into <paramref name="destination"/> by wrapping it in quotes and escaping
    /// possible quotes in the value.
    /// </summary>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer, can be the same memory region as source</param>
    /// <param name="specialCount">Amount of quotes/escapes in the source</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    sealed void EscapeField(
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        int specialCount)
    {
        Debug.Assert(destination.Length >= source.Length + specialCount + 2, "Destination buffer is too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        // Work backwards as the source and destination buffers might overlap
        int dstIndex = destination.Length - 1;
        int srcIndex = source.Length - 1;

        destination[dstIndex--] = Quote;

        while (specialCount > 0)
        {
            if (NeedsEscaping(source[srcIndex]))
            {
                destination[dstIndex--] = Escape;
                specialCount--;
            }

            destination[dstIndex--] = source[srcIndex--];
        }

        source.Slice(0, srcIndex + 1).CopyTo(destination.Slice(1));
        destination[0] = Quote;
    }

    sealed void EscapeField<TWriter>(
        ref readonly TWriter writer,
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        int specialCount,
        ArrayPool<T> arrayPool)
        where TWriter : struct, IBufferWriter<T>
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
        T[]? overflowArray = null;

        if (Token<T>.CanStackalloc(overflowLength))
        {
            overflow = stackalloc T[overflowLength];
        }
        else
        {
            overflowArray = arrayPool.Rent(overflowLength);
            overflow = overflowArray.AsSpan(0, overflowLength);
        }

        overflow[ovrIndex--] = Quote; // write closing quote

        // Short circuit to faster impl if there are no quotes in the source data
        if (specialCount == 0)
            goto CopyTo;

        // Copy tokens one-at-a-time until all quotes have been escaped and use the faster impl after
        while (ovrIndex >= 0)
        {
            if (needEscape)
            {
                overflow[ovrIndex--] = Escape;
                needEscape = false;

                if (--specialCount == 0)
                    goto CopyTo;
            }
            else if (NeedsEscaping(source[srcIndex]))
            {
                overflow[ovrIndex--] = Escape;
                srcIndex--;
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
                destination[dstIndex--] = Escape;
                needEscape = false;

                if (--specialCount == 0)
                    goto CopyTo;
            }
            else if (NeedsEscaping(source[srcIndex]))
            {
                destination[dstIndex--] = Escape;
                srcIndex--;
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
            destination[dstIndex--] = Escape;
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

        destination[dstIndex] = Quote; // write opening quote

        Debug.Assert(dstIndex == 0);
        Debug.Assert(specialCount == 0);

        // the whole of the destination is filled, with the leftovers being written to the overflow
        writer.Advance(destination.Length);

        // copy leftovers and advance
        overflow.CopyTo(writer.GetSpan(overflow.Length));
        writer.Advance(overflow.Length);

        arrayPool.EnsureReturned(ref overflowArray);
    }
}
