using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Benchmark;

public class NeedsEscapingBench
{
    public enum Input
    {
        NoneShort,
        NoneLong,
        WrappingSpace,
        WrappingTab,
        CommaShort,
        CommaLong,
        QuoteShort,
        QuoteLong,
        NewlineShort,
        NewlineLong,
    }

    [Params(
        Input.NoneShort,
        Input.NoneLong,
        Input.WrappingSpace,
        Input.WrappingTab,
        Input.CommaShort,
        Input.CommaLong,
        Input.QuoteShort,
        Input.QuoteLong,
        Input.NewlineShort,
        Input.NewlineLong)]
    public Input Data { get; set; }

    [Params("\n", "\r\n")]
    public string? Newline { get; set; }

    [Params("", " ", " \t")]
    public string? Whitespaces { get; set; }

    private string? data;
    private CsvTokens<char> tokens;

    [GlobalSetup]
    public void Setup()
    {
        tokens = new()
        {
            Delimiter = ',',
            Whitespace = Whitespaces.AsMemory(),
            NewLine = Newline.AsMemory(),
            StringDelimiter = '"',
        };

        data = Data switch
        {
            Input.NoneShort => "Short and sweet",
            Input.NoneLong => "094385324582+358308504859485983049830304980394850980345089809345098345098003",
            Input.WrappingSpace => "Testest ",
            Input.WrappingTab => "Testestest\t",
            Input.CommaShort => "A,B,C",
            Input.CommaLong => "As you can see the should be some space above, below, and to the right of the image.",
            Input.QuoteShort => "James \"007\" Bond",
            Input.QuoteLong => "jdfdgkjlsdgoijds goisjdg lkdsjfs ölkf \"kjf ölkjfdsgölkdsglkfdsöjfkg",
            Input.NewlineShort => $"crlf{Newline}",
            Input.NewlineLong => $"kjgfdjgoierpwwwwutewrtwtrpoiuwertpoiuewrpoiutlkjödfg{Newline}oifdsgfdsigufdsg",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    [Benchmark(Baseline = true)]
    public void NoEarly()
    {
        _ = Normal(data.AsSpan(), in tokens, out _);
    }

    [Benchmark]
    public void WithEarly()
    {
        _ = Optimized(data.AsSpan(), in tokens, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Optimized<T>(
        ReadOnlySpan<T> value,
        in CsvTokens<T> tokens,
        out int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        var newline = tokens.NewLine.Span;

        // For 1 token newlines we can expedite the search
        int index = newline.Length == 1
            ? value.IndexOfAny(tokens.Delimiter, tokens.StringDelimiter, newline[0])
            : value.IndexOfAny(tokens.Delimiter, tokens.StringDelimiter);

        if (index >= 0)
        {
            // we know any token before index cannot be a quote
            quoteCount = value.Slice(index).Count(tokens.StringDelimiter);
            return true;
        }

        quoteCount = 0;
        return newline.Length > 1 && value.IndexOf(newline) >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Normal<T>(
        ReadOnlySpan<T> value,
        in CsvTokens<T> tokens,
        out int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        // This check is heavier than the whitespace one, but most likely more common
        int index = value.IndexOfAny(tokens.Delimiter, tokens.StringDelimiter);

        if (index >= 0)
        {
            // we know any token before index cannot be a quote
            quoteCount = value.Slice(index).Count(tokens.StringDelimiter);
            return true;
        }

        quoteCount = 0;

        var newline = tokens.NewLine.Span;
        index = newline.Length == 1 ? value.IndexOf(newline[0]) : value.IndexOf(newline);
        return index >= 0;
    }
}
