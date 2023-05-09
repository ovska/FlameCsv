using System.Buffers;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Readers;

public static class RFC4180ModeTests
{
    [Theory]
    [InlineData("\"test\"", "test")]
    [InlineData("\"\"", "")]
    [InlineData("\"te\"\"st\"", "te\"st")]
    [InlineData("\"test\"\"\"", "test\"")]
    [InlineData("\"\"\"test\"\"\"", "\"test\"")]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"Some long, sentence\"", "Some long, sentence")]
    [InlineData("\"James \"\"007\"\" Bond\"", "James \"007\" Bond")]
    public static void Should_Unescape(string input, string expected)
    {
        using var pool = new ReturnTrackingArrayPool<char>();
        var context = new CsvReadingContext<char>(CsvTextReaderOptions.Default, new() { ArrayPool = pool });

        char[]? unescapeArray = null;
        var state = new CsvEnumerationStateRef<char>(in context, input.AsMemory(), ref unescapeArray);

        var delimiterCount = input.Count(c => c == '"');

        var actual = RFC4180Mode<char>.ReadNextField(ref state);

        Assert.Equal(expected, new string(actual.Span));

        if (actual.Span.Overlaps(unescapeArray))
        {
            Assert.NotNull(unescapeArray);
            Assert.Equal(unescapeArray.Length - actual.Length, state.buffer.Length);
            Assert.Equal(actual.ToArray(), unescapeArray.AsMemory(0, actual.Length).ToArray());
            Assert.All(unescapeArray.AsMemory(actual.Length).ToArray(), c => Assert.Equal('\0', c));
        }

        if (unescapeArray != null)
            pool.Return(unescapeArray);
    }

    [Fact]
    public static void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.AsMemory());
        var last = first.Append(end.AsMemory());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        var options = CsvDialect<char>.Default;

        Assert.True(RFC4180Mode<char>.TryGetLine(in options, ref seq, out var line, out _));

        Assert.Equal("xyz", new string(line.ToArray()));
        Assert.Equal("abc", new string(seq.ToArray()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(17)]
    [InlineData(128)]
    [InlineData(8096)]
    public static void Should_Find_Complex_Newlines(int segmentSize)
    {
        var lines = Enumerable.Range(0, 128).Select(i => new string('x', i)).ToArray();

        var joined = string.Join("\r\n", lines);

        var first = new MemorySegment<char>(lines[0].AsMemory());

        var last = joined.Chunk(segmentSize).Aggregate(first, (prev, segment) => prev.Append(segment.AsMemory()));

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        var results = new List<string>();

        while (RFC4180Mode<char>.TryGetLine(CsvDialect<char>.Default, ref seq, out var line, out _))
        {
            results.Add(new string(line.ToArray()));
        }

        results.Add(new string(seq.ToArray()));

        Assert.Equal(lines, results);
    }

    [Theory]
    [InlineData(data: new object[] { "\r\n", new[] { "abc", "\r\n", "xyz" } })]
    [InlineData(data: new object[] { "\n", new[] { "abc", "\n", "xyz" } })]
    public static void Should_Find_Segment_With_Only_Newline(string newline, string[] segments)
    {
        var options = new CsvTextReaderOptions { Newline = newline };

        var first = new MemorySegment<char>(segments[0].AsMemory());
        var last = first
            .Append(segments[1].AsMemory())
            .Append(segments[2].AsMemory());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        Assert.True(RFC4180Mode<char>.TryGetLine(new CsvDialect<char>(options), ref seq, out var firstLine, out _));
        Assert.Equal(segments[0], new string(firstLine.ToArray()));
        Assert.Equal(segments[2], new string(seq.ToArray()));
    }

    [Fact]
    public static void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var options = new CsvTextReaderOptions();
        var seq = new ReadOnlySequence<char>(data.AsMemory());

        Assert.False(RFC4180Mode<char>.TryGetLine(new CsvDialect<char>(options), ref seq, out _, out _));
        Assert.Equal(data, seq.ToArray());
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("^^^")]
    [InlineData("_-_-_")]
    [InlineData("\nnewlinex")]
    public static void Should_Find_Multitoken_Newlines(string newline)
    {
        string[] data =
        {
            "aaaa",
            "\"xyz\"",
            "\"James \"\"007\"\" Bond\"",
            "",
            "\"textwith\r\nnewline\"",
            "bb",
        };

        var dialect = CsvDialect<char>.Default with { Newline = newline.AsMemory() };
        var seq = new ReadOnlySequence<char>(string.Join(newline, data).AsMemory());

        var found = new List<string>();

        while (RFC4180Mode<char>.TryGetLine(in dialect, ref seq, out var line, out _))
        {
            found.Add(new string(line.ToArray()));
        }

        // add remaining
        found.Add(new string(seq.ToArray()));

        Assert.Equal(data, found);
    }

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("|", "", 0)]
    [InlineData("a|", "a", 0)]
    [InlineData("xyz|xyz", "xyz", 0)]
    [InlineData("\"test\"|abc", "\"test\"", 2)]
    [InlineData("\"test|\"|def", "\"test|\"", 2)]
    [InlineData("\"test|\",\"test2|\"|xyz", "\"test|\",\"test2|\"", 4)]
    [InlineData("\"a|\",\"b|\",\"c|\"|XX", "\"a|\",\"b|\",\"c|\"", 6)]
    [InlineData("\"James \"\"007\"\" Bond\"|Agent", "\"James \"\"007\"\" Bond\"", 6)]
    public static void Should_Find_Lines(string data, string expected, uint quoteCount)
    {
        var dialect = CsvDialect<char>.Default with { Newline = "|".AsMemory() };

        var seq = new ReadOnlySequence<char>(data.AsMemory());

        if (data.Contains('|'))
        {
            Assert.True(RFC4180Mode<char>.TryGetLine(in dialect, ref seq, out var line, out var meta));
            var lineStr = new string(line.ToArray());
            Assert.Equal(expected, lineStr);
            Assert.Equal(new string(seq.ToArray()), data[(lineStr.Length + 1)..]);
            Assert.Equal(quoteCount, meta.quoteCount);
        }
        else
        {
            Assert.False(RFC4180Mode<char>.TryGetLine(in dialect, ref seq, out _, out _));

            // original sequence is unchanged
            Assert.Equal(data, new string(seq.ToArray()));
        }
    }

    [Theory]
    [InlineData(",,,,")]
    [InlineData("a,b,c,d,e")]
    [InlineData("x,y,asdalksdjasd,,")]
    [InlineData(",jklsadklasdW,laskdjlksad,,1231")]
    [InlineData("A,\"B\",C,D,E")]
    [InlineData("A,\"B\",C,D,\"E\"")]
    public static void Should_Enumerate_Fields(string line)
    {
        using var pool = new ReturnTrackingArrayPool<char>();
        var options = new CsvTextReaderOptions
        {
            Newline = "|",
            ArrayPool = pool,
            AllowContentInExceptions = true,
        };

        var expected = line.Split(',').Select(s => s.Trim('"'));

        var list = new List<string>();
        var context = new CsvReadingContext<char>(options, default);

        char[]? buffer = null;

        CsvEnumerationStateRef<char> state = new(in context, line.AsMemory(), ref buffer);

        while (!state.remaining.IsEmpty)
        {
            list.Add(RFC4180Mode<char>.ReadNextField(ref state).ToString());
        }

        Assert.Equal(expected, list);

        state._context.ArrayPool.EnsureReturned(ref buffer);
    }

    [Fact]
    public static void Should_Enumerate_With_Comma2()
    {
        using var pool = new ReturnTrackingArrayPool<char>();
        var options = new CsvTextReaderOptions
        {
            Newline = "|",
            ArrayPool = pool,
            AllowContentInExceptions = true,
        };

        var data = new[] { options.Delimiter, options.Newline[0] }.GetPermutations();
        char[]? buffer = null;

        var context = new CsvReadingContext<char>(options, default);

        foreach (var chars in data)
        {
            var input = new string(chars.ToArray());
            var line = $"\"{input}\",test";

            CsvEnumerationStateRef<char> state = new(in context, line.AsMemory(), ref buffer);

            var list = new List<string>();

            while (!state.remaining.IsEmpty)
            {
                list.Add(RFC4180Mode<char>.ReadNextField(ref state).ToString());
            }

            Assert.Equal(2, list.Count);
            Assert.Equal(input, list[0]);
            Assert.Equal("test", list[1]);
        }

        context.ArrayPool.EnsureReturned(ref buffer);
    }
}
