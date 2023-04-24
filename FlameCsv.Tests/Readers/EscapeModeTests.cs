using System.Buffers;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public static class EscapeModeTests
{
    [Theory]
    [InlineData("'test'", 2, 0, "test")]
    [InlineData("te^'st", 0, 1, "te'st")]
    [InlineData("te^^st", 0, 1, "te^st")]
    public static void Should_Unescape(string input, uint quoteCount, uint escapeCount, string expected)
    {
        Memory<char> buffer = new char[128];

        Assert.Equal(
            expected,
            EscapeMode<char>.Unescape(input.AsMemory(), '\'', '^', quoteCount, escapeCount, ref buffer).ToString());
    }

    [Theory]
    [InlineData("A,B,C", new[] { "A", "B", "C" })]
    [InlineData("^A,^B,^C", new[] { "A", "B", "C" })]
    [InlineData("ASD,'ASD, DEF',A^,D", new[] { "ASD", "ASD, DEF", "A,D" })]
    [InlineData(",,", new[] { "", "", "" })]
    [InlineData("^,", new[] { "," })]
    public static void Should_Read_Columns(string input, string[] expected)
    {
        var options = new CsvTextReaderOptions { Escape = '^', Quote = '\'' };

        char[]? buffer = null;

        using (CsvEnumerationStateRefLifetime<char>.Create(options, input.AsMemory(), ref buffer, out var state))
        {
            List<string> actual = new();

            while (EscapeMode<char>.TryGetField(ref state, out ReadOnlyMemory<char> field))
                actual.Add(field.ToString());

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public static void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.AsMemory());
        var last = first.Append(end.AsMemory());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        var dialect = CsvDialect<char>.Default with { Escape = '^' };

        Assert.True(EscapeMode<char>.TryGetLine(in dialect, ref seq, out var line, out _));

        Assert.Equal("xyz", line.ToString());
        Assert.Equal("abc", seq.ToString());
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

        var dialect = CsvDialect<char>.Default with { Escape = '^' };

        var results = new List<string>();

        while (EscapeMode<char>.TryGetLine(in dialect, ref seq, out var line, out _))
        {
            results.Add(line.ToString());
        }

        results.Add(seq.ToString());

        Assert.Equal(lines, results);
    }

    [Theory]
    [InlineData(data: new object[] { "\r\n", new[] { "abc", "\r\n", "xyz" } })]
    [InlineData(data: new object[] { "\n", new[] { "abc", "\n", "xyz" } })]
    public static void Should_Find_Segment_With_Only_Newline(string newline, string[] segments)
    {
        var dialect = CsvDialect<char>.Default with { Newline = newline.AsMemory(), Escape = '^' };

        var first = new MemorySegment<char>(segments[0].AsMemory());
        var last = first
            .Append(segments[1].AsMemory())
            .Append(segments[2].AsMemory());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        Assert.True(EscapeMode<char>.TryGetLine(in dialect, ref seq, out var firstLine, out _));
        Assert.Equal(segments[0], firstLine.ToString());
        Assert.Equal(segments[2], seq.ToString());
    }

    [Fact]
    public static void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var seq = new ReadOnlySequence<char>(data.AsMemory());
        var dialect = CsvDialect<char>.Default with { Escape = '^' };

        Assert.False(EscapeMode<char>.TryGetLine(in dialect, ref seq, out _, out _));
        Assert.Equal(data, seq.ToString());
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("///")]
    [InlineData("_-_-_")]
    [InlineData("\nnewlinex")]
    public static void Should_Find_Multitoken_Newlines(string newline)
    {
        string[] data =
        {
            "aaaa",
            "\"xyz\"",
            "\"James ^\"007^\" Bond\"",
            "",
            "textwith^\r^\nnewline",
            "bb",
        };

        var dialect = CsvDialect<char>.Default with { Newline = newline.AsMemory(), Escape = '^' };
        var seq = new ReadOnlySequence<char>(string.Join(newline, data).AsMemory());

        var found = new List<string>();

        while (EscapeMode<char>.TryGetLine(in dialect, ref seq, out var line, out _))
        {
            found.Add(line.ToString());
        }

        found.Add(seq.ToString());

        Assert.Equal(data, found);
    }

    [Theory, MemberData(nameof(GetEscapeTestData))]
    public static void Should_Handle_Escapes(
        int segmentSize,
        int emptyFrequency,
        string fullLine,
        string noNewline)
    {
        var originalData = MemorySegment<char>.AsSequence(fullLine.AsMemory(), segmentSize, emptyFrequency);
        var originalWithoutNewline = MemorySegment<char>.AsSequence(noNewline.AsMemory(), segmentSize, emptyFrequency);

        string data = originalData.ToString();
        var seq = originalData;
        var dialect = CsvDialect<char>.Default with { Escape = '^', Quote = '\'' };

        string withoutNewline = originalWithoutNewline.ToString();

        Assert.True(EscapeMode<char>.TryGetLine(in dialect, ref seq, out var line, out var meta));
        Assert.Equal(0, seq.Length);
        Assert.Equal(withoutNewline, line.ToString());
        Assert.Equal(
            data.Replace("^'", "").Count(dialect.Quote),
            (int)meta.quoteCount);
        Assert.Equal(
            data.Replace("^^", "^").Count(dialect.Escape.Value),
            (int)meta.escapeCount);

        seq = originalWithoutNewline;
        Assert.True(dialect.TryGetLine(ref seq, out line, out meta, true));
        Assert.Equal(0, seq.Length);
        Assert.Equal(withoutNewline, line.ToString());
        Assert.Equal(
            data.Replace("^'", "").Count(dialect.Quote),
            (int)meta.quoteCount);
        Assert.Equal(
            data.Replace("^^", "^").Count(dialect.Escape.Value),
            (int)meta.escapeCount);
    }

    public static IEnumerable<object[]> GetEscapeTestData()
    {
        return
            from segmentSize in new[] { 1, 2, 4, 17, 128 }
            from emptyFrequency in new[] { 0, 2, 3 }
            from data in _escapeTestData.Select(x => (full: x, nolf: x[..^2]))
            select new object[]
            {
                segmentSize,
                emptyFrequency,
                data.full,
                data.nolf
            };
    }

    private static readonly string[] _escapeTestData =
    {
        "Test,Es^,caped,Es^\r\ncaped\r\n",
        "^,^'^\r\r\n",
        "^^\r\n",
        "A^,B^'^C^\r^\n\r\n",
    };
}
