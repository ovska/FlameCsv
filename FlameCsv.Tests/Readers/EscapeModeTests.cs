using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

file static class Extensions
{
    public static List<List<string>> Read(
        in this ReadOnlySequence<char> input,
        CsvOptions<char> options)
    {
        using var parser = CsvParser.Create(options, in input);

        List<List<string>> records = [];
        IMemoryOwner<char>? memoryOwner = null;
        char[] buffer = new char[64];

        while (parser.TryReadLine(out CsvFields<char> line, isFinalBlock: true) ||
               parser.TryReadLine(out line, isFinalBlock: false))
        {
            List<string> fields = [];
            var reader = new CsvFieldsRef<char>(in line, buffer);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                fields.Add(reader[i].ToString());
            }

            records.Add(fields);
        }

        memoryOwner?.Dispose();
        return records;
    }

    public static List<List<string>> Read(this string input, CsvOptions<char> options)
    {
        return Read(new ReadOnlySequence<char>(input.AsMemory()), options);
    }
}

[SuppressMessage("ReSharper", "ConvertTypeCheckPatternToNullCheck")]
public static class EscapeModeTests
{
    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test", "test")]
    [InlineData("test ", "test")]
    [InlineData(" test ", "test")]
    [InlineData("\" test\"", " test")]
    [InlineData("\"test \"", "test ")]
    [InlineData("\" test \"", " test ")]
    public static void Should_Trim_Fields(string input, string expected)
    {
        var fields = input.Read(new CsvOptions<char> { Escape = '^', Quote = '"', Whitespace = " " });
        Assert.Single(fields.SelectMany(x => x));
        Assert.Equal(expected, fields[0][0]);
    }

    [Theory]
    [InlineData("'te^'st'", "te'st")]
    [InlineData("'te^^st'", "te^st")]
    [InlineData("'^,test^,'", ",test,")]
    [InlineData("'test^,'", "test,")]
    [InlineData("'test^,^test'", "test,test")]
    [InlineData("'test^,^test^ '", "test,test ")]
    public static void Should_Unescape(string input, string expected)
    {
        string data = $"field1,field2,{input}";
        var result = data.Read(new CsvOptions<char> { Escape = '^', Quote = '\'' });
        Assert.Single(result);
        Assert.Equal(["field1", "field2", expected], result[0]);
    }

    [Theory]
    [InlineData("A,B,C", new[] { "A", "B", "C" })]
    [InlineData("'^A','^B','^C'", new[] { "A", "B", "C" })]
    [InlineData("ASD,'ASD, DEF','A^,D'", new[] { "ASD", "ASD, DEF", "A,D" })]
    [InlineData(",,", new[] { "", "", "" })]
    [InlineData("'^,'", new[] { "," })]
    [InlineData("'Test ^'Xyz^' Test'", new[] { "Test 'Xyz' Test" })]
    [InlineData("1,'^'Test^'',2", new[] { "1", "'Test'", "2" })]
    [InlineData("1,'Test','2'", new[] { "1", "Test", "2" })]
    public static void Should_Read_Fields(string input, string[] expected)
    {
        using var pool = new ReturnTrackingArrayMemoryPool<char>();
        var options = new CsvOptions<char>
        {
            Escape = '^', Quote = '\'', MemoryPool = pool,
        };

        var actual = input.Read(options);
        Assert.Single(actual);
        Assert.Equal(expected, actual[0]);
    }

    [Fact]
    public static void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.AsMemory());
        var last = first.Append(end.AsMemory());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        var result = seq.Read(new CsvOptions<char> { Newline = "\r\n", Escape = '^' });
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Sum(row => row.Count));
        Assert.Equal("xyz", result[0][0]);
        Assert.Equal("abc", result[1][0]);
    }

    [Fact]
    public static void Should_Unescape_Huge_Field()
    {
        var pt1 = new string('a', 4096);
        var pt2 = new string('b', 4096);
        var hugefield = $"\"{pt1}^\"{pt2}\"\r\n";

        var result = hugefield.Read(new CsvOptions<char> { Escape = '^', Newline = "\r\n" });
        Assert.Single(result);
        Assert.Equal([pt1 + '"' + pt2], result[0]);
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
        var result = seq.Read(new CsvOptions<char> { Newline = "\r\n", Escape = '^' });
        Assert.Equal(lines, result.Select(r => r[0]));
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
        var result = seq.Read(new CsvOptions<char> { Newline = newline, Escape = '^' });

        Assert.Equal(2, result.Count);
        Assert.Equal(segments[0], result[0][0]);
        Assert.Equal(segments[2], result[1][0]);
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
            Escape = '^', Quote = '\'', MemoryPool = pool
        };

        var fullMem = fullLine.AsMemory();
        var noNewlineMem = noNewline.AsMemory();

        using (MemorySegment<char>.Create(fullMem, segmentSize, emptyFrequency, pool, out var originalData))
        using (MemorySegment<char>.Create(noNewlineMem, segmentSize, emptyFrequency, pool, out var originalWithoutLf))
        {
            var withTrailing = originalData.Read(options);
            Assert.Single(withTrailing);

            var withoutTrailing = originalWithoutLf.Read(options);
            Assert.Single(withoutTrailing);
        }
    }

    public static TheoryData<int, int, string, string, bool?> GetEscapeTestData()
    {
        ReadOnlySpan<string> escapeData =
        [
            "'Test,Es^,caped,Es^\r\ncaped'\r\n", "'^,^'^\r'\r\n", "'^^'\r\n", "'A^,B^'^C^\r^\n'\r\n",
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
