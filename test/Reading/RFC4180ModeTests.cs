using System.Buffers;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Reading;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Reading;

public class RFC4180ModeTests
{
    [Fact]
    public void Should_Read_CRLF()
    {
        const string chars = "A,B,C\r\n1,2,3\r\n4,5,6\r\n7,8,9\r\n";
        using var reader = new CsvReader<char>(CsvOptions<char>.Default, chars.AsMemory());

        using var enumerator = reader.ParseRecords().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("A,B,C", enumerator.Current.Raw);
        Assert.Equal("A", enumerator.Current[0]);
        Assert.Equal("B", enumerator.Current[1]);
        Assert.Equal("C", enumerator.Current[2]);

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,2,3", enumerator.Current.Raw);
        Assert.Equal("1", enumerator.Current[0]);
        Assert.Equal("2", enumerator.Current[1]);
        Assert.Equal("3", enumerator.Current[2]);

        Assert.True(enumerator.MoveNext());
        Assert.Equal("4,5,6", enumerator.Current.Raw);
        Assert.Equal("4", enumerator.Current[0]);
        Assert.Equal("5", enumerator.Current[1]);
        Assert.Equal("6", enumerator.Current[2]);

        Assert.True(enumerator.MoveNext());
        Assert.Equal("7,8,9", enumerator.Current.Raw);
        Assert.Equal("7", enumerator.Current[0]);
        Assert.Equal("8", enumerator.Current[1]);
        Assert.Equal("9", enumerator.Current[2]);

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public async Task Should_Handle_Alternating_Newlines()
    {
        CsvOptions<char> options = new() { HasHeader = false, Newline = CsvNewline.CRLF };
        StringBuilder sb = new();

        foreach (int i in Enumerable.Range(0, 500))
        {
            sb.Append("field1,field2,field3");
            sb.Append(i % 2 == 0 ? "\r\n" : "\n");
        }

        // using var rb1 = new RecordBuffer();
        // using var rb2 = new RecordBuffer();
        // var tok1 = new ArmTokenizer<char, FalseConstant>(CsvOptions<char>.Default);
        // var tok2 = new SimdTokenizer<char, FalseConstant>(CsvOptions<char>.Default);
        // var dst1 = rb1.GetUnreadBuffer(tok1.MinimumFieldBufferSize, out int start1);
        // var dst2 = rb2.GetUnreadBuffer(tok2.MinimumFieldBufferSize, out int start2);
        // var cnt1 = tok1.Tokenize(dst1, start1, sb.ToString());
        // var cnt2 = tok2.Tokenize(dst2, start2, sb.ToString());
        // var min = Math.Min(cnt1, cnt2);
        // Assert.Equal(rb2._fields.AsSpan(0, min + 1), rb1._fields.AsSpan(0, min + 1));
        var expected = Enumerable.Repeat(("field1", "field2", "field3"), 500);

        var result = StringBuilderSegment.Create(sb).Read(options);
        Assert.Equal(expected, Csv.From(sb).Read<(string, string, string)>(options));

        // ensure parallel falls back correctly too
        // TODO: remove rent when sequence can be used in parallel
        using var mo = MemoryOwner<char>.Allocate(sb.Length);
        sb.CopyTo(0, mo.Memory.Span, sb.Length);

        var fromParallel = Csv.From(mo.Memory[..sb.Length])
            .AsParallel(TestContext.Current.CancellationToken)
            .ReadUnordered<(string, string, string)>(options)
            .SelectMany(chunk => chunk);
        Assert.Equal(expected, fromParallel);

        fromParallel = (
            await Csv.From(mo.Memory[..sb.Length])
                .AsParallel(TestContext.Current.CancellationToken)
                .ReadUnorderedAsync<(string, string, string)>(options)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
        ).SelectMany(chunk => chunk);
        Assert.Equal(expected, fromParallel);
    }

    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test", "test")]
    [InlineData("test ", "test")]
    [InlineData(" test ", "test")]
    [InlineData("\" test\"", " test")]
    [InlineData("\"test \"", "test ")]
    [InlineData("\" test \"", " test ")]
    [InlineData("  \"  test  \"  ", "  test  ")]
    public void Should_Trim_Fields(string input, string expected)
    {
        var result = input.Read(new CsvOptions<char> { Trimming = CsvFieldTrimming.Both });
        Assert.Single(result);
        Assert.Equal([expected], result[0]);
    }

    [Fact]
    public void Should_Seek_Long_Line()
    {
        const string input = "\"Long line with lots of content, but no quotes except the wrapping!\"";
        var result = input.Read(CsvOptions<char>.Default);

        Assert.Single(result);
        Assert.Equal([input[1..^1]], result[0]);
    }

    [Theory]
    [InlineData("\"test\"", "test")]
    [InlineData("\"\"", "")]
    [InlineData("\"te\"\"st\"", "te\"st")]
    [InlineData("\"test\"\"\"", "test\"")]
    [InlineData("\"\"\"test\"\"\"", "\"test\"")]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"Some long, sentence\"", "Some long, sentence")]
    [InlineData("\"James \"\"007\"\" Bond\"", "James \"007\" Bond")]
    public void Should_Unescape(string input, string expected)
    {
        string data = $"field1,field2,{input}";
        var result = data.Read(CsvOptions<char>.Default);
        Assert.Equal(
            [
                ["field1", "field2", expected],
            ],
            result
        );
    }

    [Fact]
    public void Should_Handle_Segment_With_Only_CarriageReturn()
    {
        var data = MemorySegment.Create("some,line,here", "\r", "\n");
        var result = data.Read(CsvOptions<char>.Default);

        Assert.Single(result);
        Assert.Equal(["some", "line", "here"], result[0]);
    }

    [Fact]
    public void Should_Unescape_Huge_Field()
    {
        var pt1 = new string('a', 4096);
        var pt2 = new string('b', 4096);
        var hugefield = $"\"{pt1}\"\"{pt2}\"\r\n";
        Assert.Equal(8198, hugefield.Length);

        var result = hugefield.Read(new CsvOptions<char> { Newline = CsvNewline.CRLF });
        Assert.Single(result);
        Assert.Equal([pt1 + '"' + pt2], result[0]);
    }

    [Fact]
    public void Should_Unescape_Field_With_Many_Quotes()
    {
        var quotes = new string('"', 1000);
        var field = $"\"{quotes}\"";
        var result = field.Read(CsvOptions<char>.Default);
        Assert.Equal(
            [
                [new string('"', 500)],
            ],
            result
        );
    }

    [Fact]
    public void Should_Trim_Outside_Quotes()
    {
        var data = "  \"  spaced field  \"  \n";
        var result = data.Read(new CsvOptions<char> { Trimming = CsvFieldTrimming.Both });
        Assert.Single(result);
        Assert.Equal(["  spaced field  "], result[0]);
    }
}

file static class Extensions
{
    public static List<List<string>> Read(in this ReadOnlySequence<char> input, CsvOptions<char> options)
    {
        List<List<string>> records = [];

        foreach (var reader in new CsvReader<char>(options, in input).ParseRecords())
        {
            List<string> fields = [];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                fields.Add(reader[i].ToString());
            }

            records.Add(fields);
        }

        return records;
    }

    public static List<List<string>> Read(this string input, CsvOptions<char> options)
    {
        return Read(new ReadOnlySequence<char>(input.AsMemory()), options);
    }
}
