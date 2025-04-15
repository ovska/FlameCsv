using System.Buffers;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public class CsvParserTests
{
    private const string Data =
        """"
        1,Alice,true,1.0,"Helsinki, Finland"
        2,Bob,false,2.0,"Stockholm, Sweden"
        3,"Jason ""The Great""",true,3.0,"Oslo, Norway"

        """";

    public static TheoryData<NewlineToken, Mode, bool> ReadLineData
    {
        get
        {
            var data = new TheoryData<NewlineToken, Mode, bool>();
            foreach (var newline in GlobalData.Enum<NewlineToken>())
            foreach (var mode in (Mode[]) [Mode.RFC, Mode.Escape])
            foreach (var trailingNewline in GlobalData.Booleans)
            {
                data.Add(newline, mode, trailingNewline);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ReadLineData))]
    public void Should_Read_Lines(NewlineToken newline, Mode mode, bool trailingNewline)
    {
        string nlt = newline == NewlineToken.LF ? "\n" : "\r\n";
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
                """);
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

        var parser = CsvParser.Create(
            new CsvOptions<char>
            {
                NoReadAhead = true,
                Newline = newline == NewlineToken.LF ? "\n" : "\r\n",
                Escape = mode == Mode.Escape ? '^' : null,
            },
            CsvPipeReader.Create(MemorySegment<char>.AsSequence(data.AsMemory(), 64)));

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

    [Fact]
    public void Should_Handle_Empty_Lines()
    {
        const string data =
            """
            1,2,3

            4,5,6

            """;

        var parser = CsvParser.Create(
            new CsvOptions<char> { Newline = "\n" },
            new ReadOnlySequence<char>(data.AsMemory()));

        using var enumerator = parser.ParseRecords().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,2,3", enumerator.Current.Record.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.GetRecordLength());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("4,5,6", enumerator.Current.Record.ToString());

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

        await using var reader = CsvPipeReader.Create(
            new StreamReader(
                new MemoryStream(data),
                Encoding.UTF8,
                bufferSize: data.Length - 1));

        CsvReadResult<char> result = await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Encoding.UTF8.GetString(data).ToCharArray(), result.Buffer.ToArray());

        var parser = CsvParser.Create(CsvOptions<char>.Default, reader);

        await using var enumerator = parser
            .ParseRecordsAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator();

        Assert.False(parser.TryGetBuffered(out _));

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("1,2,3", enumerator.Current.Record.ToString());

        // buffer is cleared on Advance
        Assert.True(await parser.TryAdvanceReaderAsync(TestContext.Current.CancellationToken));

        Assert.False(parser.TryGetBuffered(out _)); // buffer is cleared on Advance
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("4,5,6", enumerator.Current.Record.ToString());

        // no trailing newline
        Assert.False(parser.TryGetBuffered(out _));
        Assert.False(parser.TryReadLine(out _, isFinalBlock: false));

        Assert.True(parser.TryReadLine(out var line, isFinalBlock: true));
        Assert.Equal("7,8,9", line.Record.ToString());
    }

    [Fact]
    public void Should_Reset()
    {
        (ICsvPipeReader<char> reader, bool resetable)[] charData =
        [
            (CsvPipeReader.Create(ReadOnlySequence<char>.Empty), true),
            (CsvPipeReader.Create(new StringBuilder("test")), true),
            (CsvPipeReader.Create(new StringReader("wrapped in constant")), true),
            (CsvPipeReader.Create(new StreamReader(new MemoryStream())), false),
            (CsvPipeReader.Create(new MemoryStream(), Encoding.UTF8), false),
        ];

        foreach ((ICsvPipeReader<char> reader, bool resetable) in charData)
        {
            using var parser = CsvParser.Create(CsvOptions<char>.Default, reader);
            Assert.Equal(resetable, parser.TryReset());
        }

        (ICsvPipeReader<byte> reader, bool resetable)[] byteData =
        [
            (CsvPipeReader.Create(ReadOnlySequence<byte>.Empty), true),
            (CsvPipeReader.Create(new MemoryStream()), true),
            (CsvPipeReader.Create(new MemoryStream([1, 2, 3])), true),
            (CsvPipeReader.Create(new GZipStream(Stream.Null, CompressionMode.Decompress)), false),
            (new PipeReaderWrapper(PipeReader.Create(new MemoryStream())), false), // pipereader is not resetable
        ];

        foreach ((ICsvPipeReader<byte> reader, bool resetable) in byteData)
        {
            using var parser = CsvParser.Create(CsvOptions<byte>.Default, reader);
            Assert.Equal(resetable, parser.TryReset());
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Should_Skip_Whitespace_Last_Line(bool unix)
    {
        const string data = "A,B,C\nD,E,F\n   ";

        CsvOptions<char> options = new()
        {
            Trimming = CsvFieldTrimming.Both, Newline = "\n", Escape = unix ? '\\' : null,
        };

        List<string> lines = [];

        // ReSharper disable once NotDisposedResource
        foreach (var line in CsvParser.Create(options, CsvPipeReader.Create(data.AsMemory())).ParseRecords())
        {
            lines.Add(line.Record.ToString());
        }

        Assert.Equal(["A,B,C", "D,E,F"], lines);
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

        var parser = CsvParser.Create(CsvOptions<byte>.Default, CsvPipeReader.Create(data.AsMemory()));

        List<byte[]> results = [];

        foreach (var line in parser.ParseRecords())
        {
            results.Add(line.Record.ToArray());
        }

        Assert.Equal(["Hello", "World!"], results.Select(Encoding.UTF8.GetString));
    }
}
