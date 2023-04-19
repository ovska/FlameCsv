using System.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests;

public static class Compliance
{
    [Fact]
    public static void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.ToCharArray());
        var last = first.Append(end.ToCharArray());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        var options = CsvDialect<char>.Default;

        Assert.True(LineReader.TryGetLine(in options, ref seq, out var line, out _, false));

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

        while (LineReader.TryGetLine(CsvDialect<char>.Default, ref seq, out var line, out _, false))
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

        var first = new MemorySegment<char>(segments[0].ToCharArray());
        var last = first
            .Append(segments[1].ToCharArray())
            .Append(segments[2].ToCharArray());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        Assert.True(LineReader.TryGetLine(new CsvDialect<char>(options), ref seq, out var firstLine, out _, false));
        Assert.Equal(segments[0], new string(firstLine.ToArray()));
        Assert.Equal(segments[2], new string(seq.ToArray()));
    }

    [Fact]
    public static void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var options = new CsvTextReaderOptions();
        var seq = new ReadOnlySequence<char>(data.ToCharArray());

        Assert.False(LineReader.TryGetLine(new CsvDialect<char>(options), ref seq, out _, out _, false));
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

        var options = CsvDialect<char>.Default.Clone(newline: newline.AsMemory());
        var seq = new ReadOnlySequence<char>(string.Join(newline, data).ToCharArray());

        var found = new List<string>();

        while (LineReader.TryGetLine(in options, ref seq, out var line, out _, false))
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
    public static void Should_Find_Lines(string data, string expected, int expectedDelimiterCount)
    {
        var options = CsvDialect<char>.Default.Clone(newline: "|".AsMemory());

        var seq = new ReadOnlySequence<char>(data.ToCharArray());

        if (data.Contains('|'))
        {
            Assert.True(LineReader.TryGetLine(in options, ref seq, out var line, out var strCount, false));
            var lineStr = new string(line.ToArray());
            Assert.Equal(expected, lineStr);
            Assert.Equal(new string(seq.ToArray()), data[(lineStr.Length + 1)..]);
            Assert.Equal(expectedDelimiterCount, strCount);
        }
        else
        {
            Assert.False(LineReader.TryGetLine(in options, ref seq, out _, out _, false));

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
        var options = CsvDialect<char>.Default.Clone(newline: "|".AsMemory());

        char[]? buffer = null;

        var enumerator = new CsvColumnEnumerator<char>(
            line,
            options,
            5,
            line.Count(c => c == '"'),
            new BufferOwner<char>(ref buffer, AllocatingArrayPool<char>.Instance));

        foreach (var current in enumerator)
        {
            list.Add(current.ToString());
        }

        Assert.Equal(expected, list);
    }

    [Fact]
    public static void Should_Enumerate_With_Comma2()
    {
        var dialect = CsvDialect<char>.Default.Clone(newline: "|".AsMemory());

        var data = new[] { dialect.Delimiter, dialect.Newline.Span[0] }.GetPermutations();
        char[]? buffer = null;

        foreach (var chars in data)
        {
            var input = new string(chars.ToArray());
            var line = $"\"{input}\",test";

            var enumerator = new CsvColumnEnumerator<char>(
                line,
                in dialect,
                2,
                line.Count(c => c == dialect.Quote),
                new BufferOwner<char>(ref buffer, AllocatingArrayPool<char>.Instance));

            var list = new List<string>();

            foreach (var current in enumerator)
            {
                list.Add(current.ToString());
            }

            Assert.Equal(2, list.Count);
            Assert.Equal(input, list[0]);
            Assert.Equal("test", list[1]);
        }
    }
}
