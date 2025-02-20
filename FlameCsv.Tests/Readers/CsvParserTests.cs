using System.Buffers;
using System.Text;
using FlameCsv.Exceptions;
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
        string nlt = newline switch
        {
            NewlineToken.LF or NewlineToken.AutoLF => "\n",
            _ => "\r\n",
        };

        string data = Data.Replace("\n", nlt);

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

        using var parser = CsvParser.Create(
            new CsvOptions<char>
            {
                NoReadAhead = true,
                Newline = newline switch
                {
                    NewlineToken.CRLF => "\r\n",
                    NewlineToken.LF => "\n",
                    _ => null,
                },
                Escape = mode == Mode.Escape ? '^' : null,
            },
            CsvPipeReader.Create(MemorySegment<char>.AsSequence(data.AsMemory(), 64)));

        using var enumerator = parser.GetEnumerator();

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
    public void Should_Fail_If_Autodetected_Newline_Not_Found()
    {
        using var parser = CsvParser.Create(
            new CsvOptions<char> { Newline = null },
            new ReadOnlySequence<char>(new string('x', 4096).AsMemory()));
        // ReSharper disable once GenericEnumeratorNotDisposed
        Assert.Throws<CsvFormatException>(() => parser.GetEnumerator().MoveNext());
    }

    [Fact]
    public void Should_Handle_Empty_Lines()
    {
        const string data =
            """
            1,2,3

            4,5,6

            """;

        using var parser = CsvParser.Create(
            new CsvOptions<char> { Newline = "\n" },
            new ReadOnlySequence<char>(data.AsMemory()));

        using var enumerator = parser.GetEnumerator();

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

        CsvReadResult<char> result = await reader.ReadAsync();

        Assert.Equal(Encoding.UTF8.GetString(data).ToCharArray(), result.Buffer.ToArray());

        await using var parser = CsvParser.Create(CsvOptions<char>.Default, reader);

        await using var enumerator = parser.GetAsyncEnumerator();

        Assert.False(parser.TryGetBuffered(out _));

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("1,2,3", enumerator.Current.Record.ToString());

        // buffer is cleared on Advance
        Assert.True(await parser.TryAdvanceReaderAsync());

        Assert.False(parser.TryGetBuffered(out _)); // buffer is cleared on Advance
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("4,5,6", enumerator.Current.Record.ToString());

        // no trailing newline
        Assert.False(parser.TryGetBuffered(out _));
        Assert.False(parser.TryReadLine(out _, isFinalBlock: false));

        Assert.True(parser.TryReadLine(out var line, isFinalBlock: true));
        Assert.Equal("7,8,9", line.Record.ToString());
    }
}
