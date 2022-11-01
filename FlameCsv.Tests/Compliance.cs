using System.Buffers;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests;

public class Compliance
{
    [Fact]
    public static void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.ToCharArray());
        var last = first.Append(end.ToCharArray());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        var options = CsvParserOptions<char>.Windows;

        Assert.True(LineReader.TryRead(in options, ref seq, out var line, out _));

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
        var options = CsvParserOptions<char>.Windows;

        var results = new List<string>();

        while (LineReader.TryRead(in options, ref seq, out var line, out _))
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
        var options = CsvParserOptions<char>.Windows with { NewLine = newline.ToCharArray() };

        var first = new MemorySegment<char>(segments[0].ToCharArray());
        var last = first
            .Append(segments[1].ToCharArray())
            .Append(segments[2].ToCharArray());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        Assert.True(LineReader.TryRead(in options, ref seq, out var firstLine, out _));
        Assert.Equal(segments[0], new string(firstLine.ToArray()));
        Assert.Equal(segments[2], new string(seq.ToArray()));
    }

    [Fact]
    public static void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var options = CsvParserOptions<char>.Windows;
        var seq = new ReadOnlySequence<char>(data.ToCharArray());

        Assert.False(LineReader.TryRead(in options, ref seq, out _, out _));
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

        var options = CsvParserOptions<char>.Windows with { NewLine = newline.ToCharArray() };
        var seq = new ReadOnlySequence<char>(string.Join(newline, data).ToCharArray());

        var found = new List<string>();

        while (LineReader.TryRead(in options, ref seq, out var line, out _))
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
        var options = CsvParserOptions<char>.Environment with { NewLine = new[] { '|' } };

        var seq = new ReadOnlySequence<char>(data.ToCharArray());

        if (data.Contains('|'))
        {
            Assert.True(LineReader.TryRead(in options, ref seq, out var line, out var strCount));
            var lineStr = new string(line.ToArray());
            Assert.Equal(expected, lineStr);
            Assert.Equal(new string(seq.ToArray()), data[(lineStr.Length + 1)..]);
            Assert.Equal(expectedDelimiterCount, strCount);
        }
        else
        {
            Assert.False(LineReader.TryRead(in options, ref seq, out _, out _));

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
    public static void Should_Enumerate_Columns(string line)
    {
        var expected = line.Split(',').Select(s => s.Trim('"'));

        var list = new List<string>();
        var options = CsvParserOptions<char>.Unix with { NewLine = "|".AsMemory() };

        using var bo = new BufferOwner<char>();
        var enumerator = new CsvColumnEnumerator<char>(
            line,
            options,
            5,
            line.Count(c => c == '"'),
            bo);

        foreach (var current in enumerator)
        {
            list.Add(current.ToString());
        }

        Assert.Equal(expected, list);
    }
}
