using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[SimpleJob]
//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class ModeBench
{
    private static readonly CsvReadingContext<byte> _context = new(CsvUtf8Options.Default, default);
    private static readonly (ReadOnlyMemory<byte> data, RecordMeta meta)[] _bytes
        = File.ReadAllLines(
            "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
            Encoding.UTF8)
        .Select(Encoding.UTF8.GetBytes)
        .Select(b => (new ReadOnlyMemory<byte>(b), _context.GetRecordMeta(b)))
        .ToArray();

    //[Benchmark(Baseline = true)]
    //public void Run()
    //{
    //    byte[]? array = null;

    //    foreach (ref readonly var tuple in _bytes.AsSpan())
    //    {
    //        CsvEnumerationStateRef<byte> state = new(in _context, tuple.data, ref array, tuple.meta);

    //        while (!state.remaining.IsEmpty)
    //            _ = RFCOLD<byte>(ref state);
    //    }

    //    _context.ArrayPool.EnsureReturned(ref array);
    //}

    //[Benchmark(Baseline = true)]
    //public void Old()
    //{
    //    byte[]? array = null;

    //    foreach (ref readonly var tuple in _bytes.AsSpan())
    //    {
    //        CsvEnumerationStateRef<byte> state = new(in _context, tuple.data, ref array, tuple.meta);

    //        while (!state.remaining.IsEmpty)
    //            _ = RFCOLD<byte>(ref state);
    //    }

    //    _context.ArrayPool.EnsureReturned(ref array);
    //}

    [Benchmark(Baseline = true)]
    public void Old()
    {
        byte[]? array = null;

        foreach (ref readonly var tuple in _bytes.AsSpan())
        {
            CsvEnumerationStateRef<byte> state = new(in _context, tuple.data, ref array, tuple.meta);

            while (!state.remaining.IsEmpty)
                _ = RFC4180ModeOld<byte>.ReadNextField(ref state);
        }

        _context.ArrayPool.EnsureReturned(ref array);
    }

    [Benchmark(Baseline = false)]
    public void New()
    {
        byte[]? array = null;

        foreach (ref readonly var tuple in _bytes.AsSpan())
        {
            CsvEnumerationStateRef<byte> state = new(in _context, tuple.data, ref array, tuple.meta);

            while (!state.remaining.IsEmpty)
                _ = RFC4180Mode<byte>.ReadNextField(ref state);
        }

        _context.ArrayPool.EnsureReturned(ref array);
    }
}

internal static partial class RFC4180ModeOld<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Reads the next field from a <strong>non-empty</strong> state.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlyMemory<T> ReadNextField(ref CsvEnumerationStateRef<T> state)
    {
        Debug.Assert(!state.remaining.IsEmpty);

        ReadOnlyMemory<T> field;
        ReadOnlySpan<T> span = state.remaining.Span;
        T quote = state._context.Dialect.Quote;
        T delimiter = state._context.Dialect.Delimiter;
        int consumed = 0;
        uint quotesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;

        if (!state.isAtStart && !span[consumed++].Equals(delimiter))
        {
            state.ThrowNoDelimiterAtHead();
        }

        if (quotesRemaining == 0)
            goto ContinueReadNoQuotes;

        ContinueReadUnknown:
        while (consumed < span.Length)
        {
            T token = span.DangerousGetReferenceAt(consumed++);

            if (token.Equals(delimiter))
            {
                goto Done;
            }
            else if (token.Equals(quote))
            {
                quotesConsumed++;
                quotesRemaining--;
                goto ContinueReadInsideQuotes;
            }
        }

        goto EOL;

        ContinueReadInsideQuotes:
        while (consumed < span.Length)
        {
            if (span.DangerousGetReferenceAt(consumed++).Equals(quote))
            {
                quotesConsumed++;
                quotesRemaining--;

                if (quotesRemaining == 0)
                    goto ContinueReadNoQuotes;

                goto ContinueReadUnknown;
            }
        }

        goto EOL;

        ContinueReadNoQuotes:
        while (consumed < span.Length)
        {
            if (span.DangerousGetReferenceAt(consumed++).Equals(delimiter))
            {
                goto Done;
            }
        }

        EOL:
        if (quotesRemaining != 0)
            state.ThrowForInvalidEOF();

        // whole line was consumed, skip the delimiter if it wasn't the first field
        field = state.remaining.Slice((!state.isAtStart).ToByte());
        state.remaining = default;
        goto Return;

        Done:
        int sliceStart = (!state.isAtStart).ToByte();
        int length = consumed - sliceStart - 1;
        field = state.remaining.Slice(sliceStart, length);
        state.remaining = state.remaining.Slice(consumed - 1);

        Return:
        state.isAtStart = false;

        if (!state._context.Dialect.Whitespace.IsEmpty)
            field = field.Trim(state._context.Dialect.Whitespace.Span);

        return quotesConsumed == 0 ? field : Unescape(field, quote, quotesConsumed, ref state.buffer);
    }

    /// <summary>
    /// Unescapes wrapping and inner double quotes from the input.
    /// The input must be wrapped in quotes, and other quotes in the input must be in pairs
    /// </summary>
    /// <remarks>
    /// Examples:
    /// [<c>"abc"</c>] unescapes into [<c>abc</c>], [<c>"A ""B"" C"</c>] unescapes into [<c>A "B" C</c>]
    /// </remarks>
    /// <param name="source">Data to unescape</param>
    /// <param name="quote">Double quote token</param>
    /// <param name="quoteCount">Known quote count in the data, must be over 0 and divisible by 2</param>
    /// <param name="unescapeBuffer">Buffer used if the data has quotes in-between the wrapping quotes</param>
    /// <returns>Unescaped tokens, might be a slice of the original input</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> Unescape(
        ReadOnlyMemory<T> source,
        T quote,
        uint quoteCount,
        ref Memory<T> unescapeBuffer)
    {
        Debug.Assert(quoteCount != 0 && quoteCount % 2 == 0);

        ReadOnlySpan<T> span = source.Span;

        if (span.Length >= 2 &&
            span.DangerousGetReference().Equals(quote) &&
            span.DangerousGetReferenceAt(span.Length - 1).Equals(quote))
        {
            // Trim trailing and leading quotes
            source = source.Slice(1, source.Length - 2);

            if (quoteCount != 2)
            {
                Debug.Assert(quoteCount >= 4);
                return UnescapeRare(source, quote, quoteCount - 2, ref unescapeBuffer);
            }
        }
        else
        {
            ThrowInvalidUnescape(span, quote, quoteCount);
        }

        return source;
    }

    /// <summary>
    /// Unescapes inner quotes from the input. Wrapping quotes have been trimmed at this point.
    /// </summary>
    internal static ReadOnlyMemory<T> UnescapeRare(
        ReadOnlyMemory<T> sourceMemory,
        T quote,
        uint quoteCount,
        ref Memory<T> unescapeBuffer)
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
            ThrowUnescapeBufferTooSmall(unescapedLength, unescapeBuffer.Length);

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

        ThrowInvalidUnescape(sourceMemory.Span, quote, quoteCount);
        return default; // unreachable
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnescapeBufferTooSmall(int requiredLength, int bufferLength)
    {
        throw new UnreachableException(
            $"Internal error, failed to unescape: required {requiredLength} but got buffer with length {bufferLength}.");
    }

    /// <exception cref="UnreachableException">
    /// The data and/or the supplied quote count parameter were invalid. 
    /// </exception>
    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidUnescape(
        ReadOnlySpan<T> source,
        T quote,
        uint quoteCount)
    {
        int actualCount = source.Count(quote);

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

        foreach (var token in source)
        {
            error.Append(token.Equals(quote) ? '"' : 'x');
        }

        error.Append(']');

        throw new UnreachableException(
            $"Internal error, failed to unescape (token: {typeof(T).ToTypeString()}): {error}");
    }
}
