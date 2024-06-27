using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public static class EscapeModeTests
{
    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test", "test")]
    [InlineData("test ", "test")]
    [InlineData(" test ", "test")]
    [InlineData("\" test\"", "test")]
    [InlineData("\"test \"", "test")]
    [InlineData("\" test \"", "test")]
    public static void Should_Trim_Fields(string input, string expected)
    {
        char[]? buffer = null;

        var meta = new CsvRecordMeta { quoteCount = (uint)input.Count('"') };
        var record = new CsvFieldReader<char>(
            new CsvTextOptions { Whitespace = " ", Escape = '\\' },
            input.AsMemory(),
            default,
            ref buffer,
            in meta);

        var field = UnixMode<char>.ReadNextField(ref record);

        Assert.Equal(expected, field.ToString());
        Assert.True(record.End);
        Assert.Null(buffer);
    }

    [Theory]
    [InlineData("'test'", "test", false)]
    [InlineData("te^'st", "te'st", true)]
    [InlineData("te^^st", "te^st", true)]
    public static void Should_Unescape(string input, string expected, bool usesBuffer)
    {
        char[]? buffer = null;

        using var parser = CsvParser<char>.Create(new CsvTextOptions { Escape = '^', Quote = '\'' });
        var meta = parser.GetRecordMeta(input.AsMemory());
        Span<char> stackbuffer = stackalloc char[16];

        var record = new CsvFieldReader<char>(
            parser.Options,
            input.AsMemory(),
            stackbuffer,
            ref buffer,
            in meta);

        var field = UnixMode<char>.ReadNextField(ref record);
        Assert.Equal(expected, field);
        Assert.Null(buffer);
        Assert.Equal(usesBuffer, field.Overlaps(stackbuffer, out int elementOffset));
        Assert.Equal(0, elementOffset);
    }

    [Theory]
    [InlineData("A,B,C", new[] { "A", "B", "C" })]
    [InlineData("^A,^B,^C", new[] { "A", "B", "C" })]
    [InlineData("ASD,'ASD, DEF',A^,D", new[] { "ASD", "ASD, DEF", "A,D" })]
    [InlineData(",,", new[] { "", "", "" })]
    [InlineData("^,", new[] { "," })]
    [InlineData("'Test ^'Xyz^' Test'", new[] { "Test 'Xyz' Test" })]
    [InlineData("1,'^'Test^'',2", new[] { "1", "'Test'", "2" })]
    [InlineData("1,'Test','2'", new[] { "1", "Test", "2" })]
    public static void Should_Read_Fields(string input, string[] expected)
    {
        using var pool = new ReturnTrackingArrayPool<char>();
        var options = new CsvTextOptions
        {
            Escape = '^',
            Quote = '\'',
            AllowContentInExceptions = true,
            ArrayPool = pool,
        };

        char[]? buffer = null;

        using var parser = CsvParser<char>.Create(options);
        var meta = parser.GetRecordMeta(input.AsMemory());

        var state = new CsvFieldReader<char>(
            parser.Options,
            input.AsMemory(),
            stackalloc char[64],
            ref buffer,
            in meta);

        List<string> actual = [];

        while (!state.End)
        {
            actual.Add(UnixMode<char>.ReadNextField(ref state).ToString());
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.AsMemory());
        var last = first.Append(end.AsMemory());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        using var parser = CsvParser<char>.Create(new CsvTextOptions { Newline = "\r\n", Escape = '^' });
        parser.Reset(in seq);

        Assert.True(parser.TryReadLine(out var line, out _, isFinalBlock: false));

        Assert.Equal("xyz", line.ToString());
        Assert.Equal("abc", parser._reader.UnreadSequence.ToString());
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

        using var parser = CsvParser<char>.Create(new CsvTextOptions { Newline = "\r\n", Escape = '^' });
        parser.Reset(in seq);

        var results = new List<string>();

        while (parser.TryReadLine(out var line, out _, isFinalBlock: false))
        {
            results.Add(line.ToString());
        }

        results.Add(parser._reader.UnreadSequence.ToString());

        Assert.Equal(lines, results);
    }

    [Theory]
    [InlineData(data: ["\r\n", new[] { "abc", "\r\n", "xyz" }])]
    [InlineData(data: ["\n", new[] { "abc", "\n", "xyz" }])]
    public static void Should_Find_Segment_With_Only_Newline(string newline, string[] segments)
    {
        var first = new MemorySegment<char>(segments[0].AsMemory());
        var last = first
            .Append(segments[1].AsMemory())
            .Append(segments[2].AsMemory());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        using var parser = CsvParser<char>.Create(new CsvTextOptions { Newline = newline, Escape = '^' });
        parser.Reset(in seq);

        Assert.True(parser.TryReadLine(out var line, out _, isFinalBlock: false));
        Assert.Equal(segments[0], line.ToString());
        Assert.Equal(segments[2], parser._reader.UnreadSequence.ToString());
    }

    [Fact]
    public static void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var seq = new ReadOnlySequence<char>(data.AsMemory());

        using var parser = CsvParser<char>.Create(new CsvTextOptions { Escape = '^' });
        parser.Reset(in seq);

        Assert.False(parser.TryReadLine(out _, out _, isFinalBlock: false));
        Assert.Equal(data, parser._reader.UnreadSequence.ToString());
    }

    [Theory, MemberData(nameof(GetEscapeTestData))]
    public static void Should_Handle_Escapes(
        int segmentSize,
        int emptyFrequency,
        string fullLine,
        string noNewline)
    {
        using var pool = new ReturnTrackingArrayPool<char>();
        var options = new CsvTextOptions
        {
            Escape = '^',
            Quote = '\'',
            AllowContentInExceptions = true,
            ArrayPool = pool,
        };

        var originalData = MemorySegment<char>.AsSequence(fullLine.AsMemory(), segmentSize, emptyFrequency);
        var originalWithoutNewline = MemorySegment<char>.AsSequence(noNewline.AsMemory(), segmentSize, emptyFrequency);

        string data = originalData.ToString();
        var seq = originalData;

        using var parser = CsvParser<char>.Create(options);
        parser.Reset(in seq);

        string withoutNewline = originalWithoutNewline.ToString();

        Assert.True(parser.TryReadLine(out var line, out var meta, isFinalBlock: false));
        Assert.True(parser.End);
        Assert.Equal(withoutNewline, line.ToString());
        Assert.Equal(data.Replace("^'", "").Count('\''), (int)meta.quoteCount);
        Assert.Equal(data.Replace("^^", "^").Count('^'), (int)meta.escapeCount);

        parser.Reset(in originalWithoutNewline);
        Assert.True(parser.TryReadLine(out line, out meta, isFinalBlock: true));
        Assert.True(parser.End);
        Assert.Equal(withoutNewline, line.ToString());
        Assert.Equal(data.Replace("^'", "").Count('\''), (int)meta.quoteCount);
        Assert.Equal(data.Replace("^^", "^").Count('^'), (int)meta.escapeCount);
    }

    public static TheoryData<int, int, string, string> GetEscapeTestData()
    {
        var values = from segmentSize in new[] { 1, 2, 4, 17, 128 }
                     from emptyFrequency in new[] { 0, 2, 3 }
                     from tdata in _escapeTestData.Select(x => (full: x, nolf: x[..^2]))
                     select new { segmentSize, emptyFrequency, tdata.full, tdata.nolf };

        var data = new TheoryData<int, int, string, string>();

        foreach (var x in values)
        {
            data.Add(x.segmentSize, x.emptyFrequency, x.full, x.nolf);
        }

        return data;
    }

    private static readonly string[] _escapeTestData =
    [
        "Test,Es^,caped,Es^\r\ncaped\r\n",
        "^,^'^\r\r\n",
        "^^\r\n",
        "A^,B^'^C^\r^\n\r\n",
    ];
}
