using System.Buffers;
using System.Diagnostics;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

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
        var delimiterCount = input.Count(c => c == '"');

        char[] unescapeArray = new char[input.Length * 2];
        unescapeArray.AsSpan().Fill('\0');
        Memory<char> unescapeBuffer = unescapeArray;
        ReadOnlyMemory<char> actualMemory = RFC4180Mode<char>.Unescape(input.AsMemory(), '\"', (uint)delimiterCount, ref unescapeBuffer);
        Assert.Equal(expected, new string(actualMemory.Span));

        if (actualMemory.Span.Overlaps(unescapeArray))
        {
            Assert.Equal(unescapeArray.Length - actualMemory.Length, unescapeBuffer.Length);
            Assert.Equal(actualMemory.ToArray(), unescapeArray.AsMemory(0, actualMemory.Length).ToArray());
            Assert.All(unescapeArray.AsMemory(actualMemory.Length).ToArray(), c => Assert.Equal('\0', c));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("test")]
    [InlineData("\"")]
    [InlineData("\"test")]
    [InlineData("test\"")]
    [InlineData("\"te\"st\"")]
    [InlineData("\"\"test\"")]
    [InlineData("\"test\"\"")]
    public static void Should_Throw_UnreachableException_On_Invalid(string input)
    {
        Assert.Throws<UnreachableException>(() =>
        {
            Memory<char> unused = Array.Empty<char>();
            return RFC4180Mode<char>.Unescape(input.AsMemory(), '\"', 4, ref unused);
        });
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
        var options = new CsvTextReaderOptions { Newline = newline.AsMemory() };

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
    public static void Should_Enumerate_Columns(string line)
    {
        var expected = line.Split(',').Select(s => s.Trim('"'));

        var list = new List<string>();
        var dialect = CsvDialect<char>.Default with { Newline = "|".AsMemory() };

        char[]? buffer = null;

        CsvEnumerationStateRef<char> state = new(
            dialect: in dialect,
            record: line.AsMemory(),
            remaining: line.AsMemory(),
            isAtStart: true,
            meta: dialect.GetRecordMeta(line.AsMemory(), true),
            array: ref buffer,
            arrayPool: AllocatingArrayPool<char>.Instance,
            exposeContent: true);

        while (RFC4180Mode<char>.TryGetField(ref state, out ReadOnlyMemory<char> field))
        {
            list.Add(field.ToString());
        }

        Assert.Equal(expected, list);
    }

    [Fact]
    public static void Should_Enumerate_With_Comma2()
    {
        var dialect = CsvDialect<char>.Default with { Newline = "|".AsMemory() };

        var data = new[] { dialect.Delimiter, dialect.Newline.Span[0] }.GetPermutations();
        char[]? buffer = null;

        foreach (var chars in data)
        {
            var input = new string(chars.ToArray());
            var line = $"\"{input}\",test";

            CsvEnumerationStateRef<char> state = new(
                dialect: in dialect,
                record: line.AsMemory(),
                remaining: line.AsMemory(),
                isAtStart: true,
                meta: dialect.GetRecordMeta(line.AsMemory(), true),
                array: ref buffer,
                arrayPool: AllocatingArrayPool<char>.Instance,
                exposeContent: true);

            var list = new List<string>();

            while (RFC4180Mode<char>.TryGetField(ref state, out ReadOnlyMemory<char> field))
            {
                list.Add(field.ToString());
            }

            Assert.Equal(2, list.Count);
            Assert.Equal(input, list[0]);
            Assert.Equal("test", list[1]);
        }
    }
}