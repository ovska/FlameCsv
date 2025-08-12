using System.Buffers;
using System.IO.Compression;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public class CsvReaderTests
{
    private const string Data = """"
        1,Alice,true,1.0,"Helsinki, Finland"
        2,Bob,false,2.0,"Stockholm, Sweden"
        3,"Jason ""The Great""",true,3.0,"Oslo, Norway"

        """";

    public static TheoryData<CsvNewline, Mode, bool> ReadLineData
    {
        get
        {
            var data = new TheoryData<CsvNewline, Mode, bool>();
            foreach (var newline in GlobalData.Enum<CsvNewline>())
            foreach (var mode in (Mode[])[Mode.RFC, Mode.Escape])
            foreach (var trailingNewline in GlobalData.Booleans)
            {
                data.Add(newline, mode, trailingNewline);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ReadLineData))]
    public void Should_Read_Lines(CsvNewline newline, Mode mode, bool trailingNewline)
    {
        string nlt = newline.AsString();
        string data = Data.ReplaceLineEndings(nlt);

        if (!trailingNewline)
        {
            data = data.TrimEnd(nlt.ToCharArray());
        }

        if (mode == Mode.Escape)
        {
            data = data.Replace(
                """
                ""The Great""
                """,
                """
                ^"The Great^"
                """
            );
        }

        string[] expected =
        [
            "1",
            "Alice",
            "true",
            "1.0",
            "Helsinki, Finland",
            "2",
            "Bob",
            "false",
            "2.0",
            "Stockholm, Sweden",
            "3",
            "Jason \"The Great\"",
            "true",
            "3.0",
            "Oslo, Norway",
        ];

        var parser = new CsvReader<char>(
            new CsvOptions<char> { Newline = newline, Escape = mode == Mode.Escape ? '^' : null },
            CsvBufferReader.Create(MemorySegment<char>.AsSequence(data.AsMemory(), 64))
        );

        using var enumerator = parser.ParseRecords().GetEnumerator();

        for (int lineIndex = 0; lineIndex < 3; lineIndex++)
        {
            Assert.True(enumerator.MoveNext());

            var reader = enumerator.Current;

            Assert.Equal(5, reader.FieldCount);

            for (int index = 0; index < reader.FieldCount; index++)
            {
                Assert.Equal(expected[index + (lineIndex * 5)], reader[index].ToString());
            }
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Should_Handle_Empty_Lines(bool crlf)
    {
        const string data = """
            1,2,3

            4,5,6

            """;

        var parser = new CsvReader<char>(
            new CsvOptions<char> { Newline = crlf ? CsvNewline.CRLF : CsvNewline.LF },
            new ReadOnlySequence<char>(data.ReplaceLineEndings(crlf ? "\r\n" : "\n").AsMemory())
        );

        using var enumerator = parser.ParseRecords().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,2,3", enumerator.Current.RawValue.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.GetRecordLength());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("4,5,6", enumerator.Current.RawValue.ToString());

        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public async Task Should_Handle_Advance_Before_Buffered_Lines_Read()
    {
        byte[] data =
            """
                1,2,3
                4,5,6
                7,8,9
                """u8.ToArray();

        await using var reader = CsvBufferReader.Create(
            new StreamReader(new MemoryStream(data), Encoding.UTF8, bufferSize: data.Length - 1)
        );

        CsvReadResult<char> result = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var (buffer, isCompleted) = result;

        Assert.False(isCompleted);
        Assert.Equal(Encoding.UTF8.GetString(data).ToCharArray(), buffer.ToArray());

        var parser = new CsvReader<char>(CsvOptions<char>.Default, reader);

        await using var enumerator = parser
            .ParseRecordsAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("1,2,3", enumerator.Current.RawValue.ToString());

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("4,5,6", enumerator.Current.RawValue.ToString());

        // no trailing newline
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("7,8,9", enumerator.Current.RawValue.ToString());

        Assert.False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public void Should_Reset()
    {
        (ICsvBufferReader<char> reader, bool resetable)[] charData =
        [
            (CsvBufferReader.Create(ReadOnlySequence<char>.Empty), true),
            (CsvBufferReader.Create(new StringBuilder("test")), true),
            (CsvBufferReader.Create(new StringReader("wrapped in constant")), true),
            (CsvBufferReader.Create(new StreamReader(new MemoryStream())), false),
            (CsvBufferReader.Create(new MemoryStream(), Encoding.Unicode), false),
        ];

        foreach ((ICsvBufferReader<char> reader, bool resetable) in charData)
        {
            using var parser = new CsvReader<char>(CsvOptions<char>.Default, reader);
            Assert.Equal(resetable, parser.TryReset());
        }

        (ICsvBufferReader<byte> reader, bool resetable)[] byteData =
        [
            (CsvBufferReader.Create(ReadOnlySequence<byte>.Empty), true),
            (CsvBufferReader.Create(new MemoryStream()), true),
            (CsvBufferReader.Create(new MemoryStream([1, 2, 3])), true),
            (CsvBufferReader.Create(new GZipStream(Stream.Null, CompressionMode.Decompress)), false),
        ];

        foreach ((ICsvBufferReader<byte> reader, bool resetable) in byteData)
        {
            using var parser = new CsvReader<byte>(CsvOptions<byte>.Default, reader);
            Assert.Equal(resetable, parser.TryReset());
        }
    }

    [Fact]
    public void Should_Skip_Utf8_Preamble()
    {
        byte[] data;

        using (MemoryStream ms = new())
        {
            using (StreamWriter writer = new(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                writer.Write("Hello\nWorld!");
            }

            data = ms.ToArray();
        }

        Assert.Equal(data.AsSpan(0, Encoding.UTF8.Preamble.Length), Encoding.UTF8.Preamble);

        var reader = new CsvReader<byte>(CsvOptions<byte>.Default, CsvBufferReader.Create(data.AsMemory()));

        List<byte[]> results = [];

        foreach (var line in reader.ParseRecords())
        {
            results.Add(line.RawValue.ToArray());
        }

        Assert.Equal(["Hello", "World!"], results.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void Should_Trim()
    {
        string[][] data =
        [
            [" 1 ", " 2 ", " 3 "],
            ["\" 4 \"", " 5 ", "\" 6 \""],
            [" 7 ", "  \" 8 \"  ", " 9 "],
        ];

        string csv = string.Join("\n", data.Select(line => string.Join(",", line))) + '\n';

        var options = new CsvOptions<char> { Trimming = CsvFieldTrimming.Both, Newline = CsvNewline.LF };

        List<List<string>> results = [];

        foreach (var record in new CsvReader<char>(options, csv.AsMemory()).ParseRecords())
        {
            List<string> fields = [];
            for (int i = 0; i < record.FieldCount; i++)
            {
                fields.Add(record[i].ToString());
            }
            results.Add(fields);
        }

        Assert.Equal(data.Select(line => line.Select(s => s.Trim(' ').Trim('"')).ToList()), results);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Should_Ignore_CR_On_LF_Parser(bool escape)
    {
        // large enough string so we force a SIMD path too
        string data = string.Join("\r\n", Enumerable.Repeat("1,2,3", 100)) + "\r\n";

        var options = new CsvOptions<char> { Newline = CsvNewline.LF, Escape = escape ? '\\' : null };
        using var reader = new CsvReader<char>(options, data.AsMemory());

        foreach (var record in reader.ParseRecords())
        {
            Assert.Equal(3, record.FieldCount);
            Assert.Equal("1", record[0].ToString());
            Assert.Equal("2", record[1].ToString());
            Assert.Equal("3\r", record[2].ToString());
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Should_Handle_Large_Quote_Counts(bool escape)
    {
        char c = escape ? '\\' : '"';

        StringBuilder data = new();
        data.Append("test,\"");

        for (int i = 0; i < 200; i++)
        {
            data.Append(c);
            data.Append('"');
        }

        data.Append("\",test\n");

        data.Append(data);

        foreach (
            var record in new CsvReader<char>(
                new CsvOptions<char> { Escape = escape ? c : null, Newline = CsvNewline.LF },
                data.ToString().AsMemory()
            ).ParseRecords()
        )
        {
            Assert.Equal(3, record.FieldCount);
            Assert.Equal("test", record[0].ToString());
            Assert.Equal(new string('"', 200), record[1].ToString());
            Assert.Equal("test", record[2].ToString());
        }
    }

    [Fact]
    public void Should_Throw_On_Over_65K_Field_Record()
    {
        Assert.Skip("WIP: figure out what's wrong");

        using var mo = MemoryOwner<char>.Allocate(ushort.MaxValue * 2);
        mo.Span.Fill(',');
        mo.Span[^1] = '\n';
        var options = new CsvOptions<char> { Newline = CsvNewline.LF };

        using var enumerator = CsvReader.Enumerate(mo.Memory).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(ushort.MaxValue * 2, enumerator.Current.FieldCount);
        Assert.False(enumerator.MoveNext());
    }
}
