﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Benchmark;

public class UnescapeTests
{
    private static readonly string[] _data =
    [
        "\"Wilson Jones 1\"\" Hanging DublLock® Ring Binders\"",
        "\"GBC Pre-Punched Binding Paper, Plastic, White, 8-1/2\"\" x 11\"\"\"",
        "\"GBC Twin Loop™ Wire Binding Elements, 9/16\"\" Spine, Black\"",
        "\"Tenex 46\"\" x 60\"\" Computer Anti-Static Chairmat, Rectangular Shaped\"",
        "\"#10-4 1/8\"\" x 9 1/2\"\" Premium Diagonal Seam Envelopes\"",
        "\"Wilson Jones Ledger-Size, Piano-Hinge Binder, 2\"\", Blue\"",
        "\"Executive Impressions 14\"\" Contract Wall Clock\"",
        "\"Acme Design Line 8\"\" Stainless Steel Bent Scissors w/Champagne Handles, 3-1/8\"\" Cut\"",
        "\"GE 48\"\" Fluorescent Tube, Cool White Energy Saver, 34 Watts, 30/Box\"",
    ];

    private static readonly (ReadOnlyMemory<char> value, uint quoteCount)[] _testData = _data
        .Select(s => s[1..^1])
        .Select(s => (new ReadOnlyMemory<char>(s.ToCharArray()), (uint)s.Count('"')))
        .ToArray();

    private readonly char[] _buffer = new char[1024];

    [Benchmark(Baseline = true)]
    public void Old()
    {
        Memory<char> buf = _buffer;

        foreach (ref readonly var tuple in _testData.AsSpan())
        {
            _ = UnescapeRare2(tuple.value, '"', tuple.quoteCount, ref buf);
        }
    }

    [Benchmark]
    public void New()
    {
        Memory<char> buf = _buffer;
        foreach (ref readonly var tuple in _testData.AsSpan())
        {
            _ = UnescapeRare(tuple.value, '"', tuple.quoteCount, ref buf);
        }
    }

    internal static ReadOnlyMemory<T> UnescapeRare<T>(
        ReadOnlyMemory<T> sourceMemory,
        T quote,
        uint quoteCount,
        ref Memory<T> unescapeBuffer)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);
        Debug.Assert(!unescapeBuffer.Span.Overlaps(sourceMemory.Span), "Source and unescape buffer must not overlap");

        uint quotesLeft = quoteCount;

        ReadOnlySpan<T> source = sourceMemory.Span;

        int unescapedLength = sourceMemory.Length - (int)(quoteCount / 2);

        if (unescapedLength > unescapeBuffer.Length)
            ThrowUnescapeBufferTooSmall();

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
            ThrowInvalidUnescape();

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
                    ThrowInvalidUnescape();

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
            ThrowInvalidUnescape();

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

    /// <summary>
    /// Unescapes inner quotes from the input. Wrapping quotes have been trimmed at this point.
    /// </summary>
    internal static ReadOnlyMemory<T> UnescapeRare2<T>(
        ReadOnlyMemory<T> sourceMemory,
        T quote,
        uint quoteCount,
        ref Memory<T> unescapeBuffer) where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);
        Debug.Assert(!unescapeBuffer.Span.Overlaps(sourceMemory.Span), "Source and unescape buffer must not overlap");

        int written = 0;
        int index = 0;
        uint quotesLeft = quoteCount;

        ReadOnlySpan<T> source = sourceMemory.Span;
        ReadOnlySpan<T> needle = stackalloc T[] { quote, quote };

        int unescapedLength = sourceMemory.Length - (int)(quoteCount / 2);

        if (unescapedLength > unescapeBuffer.Length)
            ThrowUnescapeBufferTooSmall();

        Memory<T> buffer = unescapeBuffer.Slice(0, unescapedLength);
        unescapeBuffer = unescapeBuffer.Slice(unescapedLength); // "consume" the buffer

        while (index < sourceMemory.Length)
        {
            int next = source.Slice(index).IndexOf(needle);

            if (next < 0)
                break;

            int toCopy = next + 1;
            sourceMemory.Slice(index, toCopy).CopyTo(buffer.Slice(written));
            written += toCopy;
            index += toCopy + 1; // advance past the second quote

            // Found all quotes, copy remaining data
            if ((quotesLeft -= 2) == 0)
            {
                sourceMemory.Slice(index).CopyTo(buffer.Slice(written));
                written += sourceMemory.Length - index;
                return buffer.Slice(0, written);
            }
        }

        ThrowInvalidUnescape();
        return default; // unreachable
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnescapeBufferTooSmall()
    {
        throw new UnreachableException();
    }

    /// <exception cref="UnreachableException">
    /// The data and/or the supplied quote count parameter were invalid. 
    /// </exception>
    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidUnescape()
    {
        throw new UnreachableException();
    }
}
