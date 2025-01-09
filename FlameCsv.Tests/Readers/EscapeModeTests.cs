using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

[SuppressMessage("ReSharper", "ConvertTypeCheckPatternToNullCheck")]
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
        IMemoryOwner<char>? allocated = null;

        var line = new CsvLine<char> { Value = input.AsMemory(), QuoteCount = (uint)input.Count('"') };
        var record = new CsvFieldReader<char>(
            new CsvOptions<char> { Whitespace = " ", Escape = '\\' },
            in line,
            default,
            ref allocated);

        var field = UnixMode<char>.ReadNextField(ref record);

        Assert.Equal(expected, field.ToString());
        Assert.True(record.End);
        Assert.Null(allocated);

        allocated?.Dispose();
    }

    [Theory]
    [InlineData("'test'", "test", false)]
    [InlineData("te^'st", "te'st", true)]
    [InlineData("te^^st", "te^st", true)]
    public static void Should_Unescape(string input, string expected, bool usesBuffer)
    {
        IMemoryOwner<char>? allocated = null;

        using var parser = CsvParser<char>.Create(new CsvOptions<char> { Escape = '^', Quote = '\'' });
        var line = parser.GetAsCsvLine(input.AsMemory());
        Span<char> stackbuffer = stackalloc char[16];

        var record = new CsvFieldReader<char>(
            parser.Options,
            in line,
            stackbuffer,
            ref allocated);

        var field = UnixMode<char>.ReadNextField(ref record);
        Assert.Equal(expected, field);
        Assert.Null(allocated);
        Assert.Equal(usesBuffer, field.Overlaps(stackbuffer, out int elementOffset));
        Assert.Equal(0, elementOffset);

        allocated?.Dispose();
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
        using var pool = new ReturnTrackingArrayMemoryPool<char>();
        var options = new CsvOptions<char>
        {
            Escape = '^', Quote = '\'', AllowContentInExceptions = true, MemoryPool = pool,
        };

        IMemoryOwner<char>? allocated = null;

        using var parser = CsvParser<char>.Create(options);
        var line = parser.GetAsCsvLine(input.AsMemory());

        var state = new CsvFieldReader<char>(
            parser.Options,
            in line,
            stackalloc char[64],
            ref allocated);

        List<string> actual = [];

        while (!state.End)
        {
            actual.Add(UnixMode<char>.ReadNextField(ref state).ToString());
        }

        Assert.Equal(expected, actual);

        allocated?.Dispose();
    }

    [Fact]
    public static void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.AsMemory());
        var last = first.Append(end.AsMemory());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        using var parser = CsvParser<char>.Create(new CsvOptions<char> { Newline = "\r\n", Escape = '^' });
        parser.Reset(in seq);

        Assert.True(parser.TryReadLine(out var line, isFinalBlock: false));

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

        using var parser = CsvParser<char>.Create(new CsvOptions<char> { Newline = "\r\n", Escape = '^' });
        parser.Reset(in seq);

        var results = new List<string>();

        while (parser.TryReadLine(out var line, isFinalBlock: false))
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
        using var parser = CsvParser<char>.Create(new CsvOptions<char> { Newline = newline, Escape = '^' });
        parser.Reset(in seq);

        Assert.True(parser.TryReadLine(out var line, isFinalBlock: false));
        Assert.Equal(segments[0], line.ToString());
        Assert.Equal(segments[2], parser._reader.UnreadSequence.ToString());
    }

    [Fact]
    public static void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var seq = new ReadOnlySequence<char>(data.AsMemory());

        using var parser = CsvParser<char>.Create(new CsvOptions<char> { Escape = '^' });
        parser.Reset(in seq);

        Assert.False(parser.TryReadLine(out _, isFinalBlock: false));
        Assert.Equal(data, parser._reader.UnreadSequence.ToString());
    }

    [Theory, MemberData(nameof(GetEscapeTestData))]
    public static void Should_Handle_Escapes(
        int segmentSize,
        int emptyFrequency,
        string fullLine,
        string noNewline,
        bool? guardedMemory)
    {
        using MemoryPool<char> pool = ReturnTrackingMemoryPool<char>.Create(guardedMemory);

        var options = new CsvOptions<char>
        {
            Escape = '^', Quote = '\'', AllowContentInExceptions = true, MemoryPool = pool,
        };

        var fullMem = fullLine.AsMemory();
        var noNewlineMem = noNewline.AsMemory();

        using (MemorySegment<char>.Create(fullMem, segmentSize, emptyFrequency, pool, out var originalData))
        using (MemorySegment<char>.Create(noNewlineMem, segmentSize, emptyFrequency, pool, out var originalWithoutLf))
        {
            string data = originalData.ToString();

            using var parser = CsvParser<char>.Create(options);
            parser.Reset(in originalData);

            string withoutNewline = originalWithoutLf.ToString();

            Assert.True(parser.TryReadLine(out var line, isFinalBlock: false));
            Assert.True(parser.End);
            Assert.Equal(withoutNewline, line.ToString());
            Assert.Equal(data.Replace("^'", "").Count('\''), (int)line.QuoteCount);
            Assert.Equal(data.Replace("^^", "^").Count('^'), (int)line.EscapeCount);

            parser.Reset(in originalWithoutLf);
            Assert.True(parser.TryReadLine(out line, isFinalBlock: true));
            Assert.True(parser.End);
            Assert.Equal(withoutNewline, line.ToString());
            Assert.Equal(data.Replace("^'", "").Count('\''), (int)line.QuoteCount);
            Assert.Equal(data.Replace("^^", "^").Count('^'), (int)line.EscapeCount);
        }
    }

    public static TheoryData<int, int, string, string, bool?> GetEscapeTestData()
    {
        ReadOnlySpan<string> escapeData =
        [
            "Test,Es^,caped,Es^\r\ncaped\r\n",
            "^,^'^\r\r\n",
            "^^\r\n",
            "A^,B^'^C^\r^\n\r\n",
        ];

        var data = new TheoryData<int, int, string, string, bool?>();

        foreach (var segmentSize in new[] { 1, 2, 4, 17, 128 })
        foreach (var emptyFrequency in new[] { 0, 2, 3 })
        foreach (var escData in escapeData)
        foreach (var guarded in GlobalData.GuardedMemory)
        {
            data.Add(segmentSize, emptyFrequency, escData, escData[..^2], guarded);
        }

        return data;
    }
}
