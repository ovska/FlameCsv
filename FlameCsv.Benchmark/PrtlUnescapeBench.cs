using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Writers;

namespace FlameCsv.Benchmark;

public enum Input
{
    Short,
    Long,
    Longest,
    Quoteless,
}

[SimpleJob]
public class PrtlUnescapeBench
{
    [Params(Input.Short, Input.Long, Input.Longest, Input.Quoteless)]
    public Input Input { get; set; }

    private char[]? _array;
#nullable disable
    private char[] destination;
    private string input;
    private int requiredLength;
    private int quoteCount;
#nullable enable

    [GlobalSetup]
    public void Setup()
    {
        // gutenberg test data
        input = Input switch
        {
            Input.Short => "A,B,C",
            Input.Long => "As you can see the should be some space above, below, and to the right of the image.",
            Input.Longest =>
                "The following image is <em><strong>wide</strong></em> (if the theme supports it, that is). If not, who knows what will happen!",
            Input.Quoteless =>
                " And just when you thought we were done we’re going to do them all over again with captions! ",
            _ => throw new ArgumentOutOfRangeException(),
        };

        // destination is always almost exactly the length of the input
        destination = new char[input.Length];

        var tokens = new CsvTokens<char> { StringDelimiter = ',', Delimiter = '|', NewLine = "\n".AsMemory() };
        if (!WriteUtil.NeedsEscaping(input.AsSpan(), in tokens, out quoteCount))
            throw new Exception("invalid test data!");
        requiredLength = input.Length + quoteCount + 2;
    }

    [Benchmark(Baseline = true)]
    public void Simple()
    {
        WithIndex(
            input.AsSpan(),
            destination,
            ',',
            requiredLength,
            quoteCount,
            ref _array,
            out _);
    }

    [Benchmark]
    public void CopyTo()
    {
        WithCopyTo(
            input.AsSpan(),
            destination,
            ',',
            requiredLength,
            quoteCount,
            ref _array,
            out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void WithCopyTo<T>(
        ReadOnlySpan<T> source,
        Span<T> destination,
        T quote,
        int requiredLength,
        int quoteCount,
        ref T[]? array,
        out int overflowLength)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(destination.Length < requiredLength, "Destination should be too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        // TODO: should this even be here? can be calculated outside
        overflowLength = requiredLength - destination.Length;
        Debug.Assert(overflowLength + destination.Length == requiredLength);

        ArrayPool<T>.Shared.EnsureCapacity(ref array, overflowLength);

        // First write the overflowing data to the array
        int srcIndex = source.Length - 1;
        int ovrIndex = overflowLength - 1;
        int dstIndex = destination.Length - 1;
        bool needQuote = false;

        Span<T> overflow = array;
        overflow[ovrIndex--] = quote;

        if (quoteCount == 0)
        {
            goto CopyTo;
        }

        while (ovrIndex >= 0)
        {
            if (needQuote)
            {
                overflow[ovrIndex--] = quote;
                needQuote = false;

                if (--quoteCount == 0)
                    goto CopyTo;
            }
            else if (source[srcIndex].Equals(quote))
            {
                overflow[ovrIndex--] = quote;
                srcIndex--;
                needQuote = true;
            }
            else
            {
                overflow[ovrIndex--] = source[srcIndex--];
            }
        }

        // TODO: optimize with CopyTo after quoteCount == 0
        while (srcIndex >= 0)
        {
            if (needQuote)
            {
                destination[dstIndex--] = quote;
                needQuote = false;

                if (--quoteCount == 0)
                    goto CopyTo;
            }
            else if (source[srcIndex].Equals(quote))
            {
                destination[dstIndex--] = quote;
                srcIndex--;
                needQuote = true;
            }
            else
            {
                destination[dstIndex--] = source[srcIndex--];
            }
        }

        // true if the first token in the source is a quote
        if (needQuote)
        {
            destination[dstIndex--] = quote;
            quoteCount--;
        }

        CopyTo:
        if (srcIndex > 0)
        {
            if (ovrIndex >= 0)
            {
                source.Slice(srcIndex, ovrIndex + 1).CopyTo(overflow);
                srcIndex -= ovrIndex + 1;
            }

            if (dstIndex > 1)
            {
                source.Slice(0, srcIndex + 1).CopyTo(destination.Slice(1));
                dstIndex = 0;
            }
        }

        destination[dstIndex] = quote;

        Debug.Assert(dstIndex == 0);
        Debug.Assert(quoteCount == 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void WithIndex<T>(
        ReadOnlySpan<T> source,
        Span<T> destination,
        T quote,
        int requiredLength,
        int quoteCount,
        ref T[]? array,
        out int overflowLength)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(destination.Length < requiredLength, "Destination should be too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        // TODO: should this even be here? can be calculated outside
        overflowLength = requiredLength - destination.Length;
        Debug.Assert(overflowLength + destination.Length == requiredLength);

        ArrayPool<T>.Shared.EnsureCapacity(ref array, overflowLength);

        // First write the overflowing data to the array
        int srcIndex = source.Length - 1;
        int ovrIndex = overflowLength - 1;
        bool needQuote = false;

        Span<T> overflow = array;
        overflow[ovrIndex--] = quote;

        while (ovrIndex >= 0)
        {
            if (needQuote)
            {
                overflow[ovrIndex--] = quote;
                needQuote = false;
                quoteCount--;
            }
            else if (source[srcIndex].Equals(quote))
            {
                overflow[ovrIndex--] = quote;
                srcIndex--;
                needQuote = true;
            }
            else
            {
                overflow[ovrIndex--] = source[srcIndex--];
            }
        }

        int dstIndex = destination.Length - 1;

        // TODO: optimize with CopyTo after quoteCount == 0
        while (srcIndex >= 0)
        {
            if (needQuote)
            {
                destination[dstIndex--] = quote;
                needQuote = false;
                quoteCount--;
            }
            else if (source[srcIndex].Equals(quote))
            {
                destination[dstIndex--] = quote;
                srcIndex--;
                needQuote = true;
            }
            else
            {
                destination[dstIndex--] = source[srcIndex--];
            }
        }


        // true if the first token in the source is a quote
        if (needQuote)
        {
            destination[dstIndex--] = quote;
            quoteCount--;
        }

        destination[dstIndex] = quote;

        Debug.Assert(dstIndex == 0);
        Debug.Assert(quoteCount == 0);
    }
}
