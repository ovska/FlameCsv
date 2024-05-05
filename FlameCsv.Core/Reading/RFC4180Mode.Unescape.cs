using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlameCsv.Reading;

internal static partial class RFC4180Mode<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Unescapes inner quotes from the input. Wrapping quotes have been trimmed at this point.
    /// </summary>
    internal static ReadOnlyMemory<T> Unescape(
        ReadOnlyMemory<T> sourceMemory,
        T quote,
        uint quoteCount,
        ref Memory<T> unescapeBuffer)
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);
        Debug.Assert(!unescapeBuffer.Span.Overlaps(sourceMemory.Span), "Source and unescape buffer must not overlap");

        uint quotesLeft = quoteCount;

        ReadOnlySpan<T> source = sourceMemory.Span;

        int unescapedLength = sourceMemory.Length - (int)(quoteCount / 2);

        if (unescapedLength > unescapeBuffer.Length)
            ThrowUnescapeBufferTooSmall(unescapedLength, unescapeBuffer.Length);

        Memory<T> buffer = unescapeBuffer.Slice(0, unescapedLength);
        unescapeBuffer = unescapeBuffer.Slice(unescapedLength); // "consume" the buffer

        nuint srcIndex = 0;
        nuint srcLength = (nuint)source.Length;
        nuint dstIndex = 0;

        ref T src = ref source.DangerousGetReference();
        ref T dst = ref buffer.Span.DangerousGetReference();

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
        goto FoundLong;

        FoundLong:
        if (srcIndex >= srcLength || !quote.Equals(Unsafe.Add(ref src, srcIndex)))
            ThrowInvalidUnescape(sourceMemory, quote, quoteCount);

        srcIndex++;

        quotesLeft -= 2;

        if (quotesLeft == 0)
            goto NoQuotesLeft;

        ContinueRead:
        while ((srcLength - srcIndex) >= 8)
        {
            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 0)))
                goto Found1;

            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 1)))
                goto Found2;

            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 2)))
                goto Found3;

            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 3)))
                goto Found4;

            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 4)))
                goto Found5;

            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 5)))
                goto Found6;

            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 6)))
                goto Found7;

            if (quote.Equals(Unsafe.Add(ref src, srcIndex + 7)))
                goto Found8;

            srcIndex += 8;
            dstIndex += 8;
        }

        while (srcIndex < srcLength)
        {
            if (quote.Equals(Unsafe.Add(ref src, srcIndex)))
            {
                srcIndex++;

                if (srcIndex >= srcLength || !quote.Equals(Unsafe.Add(ref src, srcIndex)))
                    ThrowInvalidUnescape(sourceMemory, quote, quoteCount);

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
            ThrowInvalidUnescape(sourceMemory, quote, quoteCount);

        return buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, nuint srcIndex, ref T dst, nuint dstIndex, uint length)
        {
            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length / (uint)Unsafe.SizeOf<byte>());
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowUnescapeBufferTooSmall(int requiredLength, int bufferLength)
    {
        throw new UnreachableException(
            $"Internal error, failed to unescape: required {requiredLength} but got buffer with length {bufferLength}.");
    }

    /// <exception cref="UnreachableException">
    /// The data and/or the supplied quote count parameter were invalid. 
    /// </exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidUnescape(
        ReadOnlyMemory<T> source,
        T quote,
        uint quoteCount)
    {
        int actualCount = System.MemoryExtensions.Count(source.Span, quote);

        var error = new StringBuilder(64);

        if (source.Length < 2)
        {
            error.Append(CultureInfo.InvariantCulture, $"Source is too short (length: {source.Length}). ");
        }

        if (actualCount != quoteCount)
        {
            error.Append(CultureInfo.InvariantCulture, $"String delimiter count {quoteCount} was invalid (actual was {actualCount}). ");
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in source.Span)
        {
            error.Append(token.Equals(quote) ? '"' : 'x');
        }

        error.Append(']');

        throw new UnreachableException(
            $"Internal error, failed to unescape (token: {typeof(T).ToTypeString()}): {error}");
    }
}
