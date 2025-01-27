using System.Buffers;
using FlameCsv.Exceptions;
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
            });

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

        parser.Reset(MemorySegment<char>.AsSequence(data.AsMemory(), 64));
        var buffer = new char[64];

        for (int lineIndex = 0; lineIndex < 3; lineIndex++)
        {
            Assert.True(parser.TryReadLine(out var line, isFinalBlock: lineIndex == 2 && !trailingNewline));

            var reader = new MetaFieldReader<char>(in line, unescapeBuffer: buffer);

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
        using var parser = CsvParser.Create(new CsvOptions<char> { Newline = null });
        parser.Reset(new ReadOnlySequence<char>(new string('x', 4096).AsMemory()));
        Assert.Throws<CsvFormatException>(() => parser.TryReadLine(out _, false));
    }

    [Fact]
    public void Should_Handle_Empty_Lines()
    {
        const string data =
            """
            1,2,3

            4,5,6

            """;

        using var parser = CsvParser.Create(new CsvOptions<char> { Newline = "\n" });
        parser.Reset(new(data.AsMemory()));

        Assert.True(parser.TryReadLine(out var line, isFinalBlock: false));
        Assert.Equal("1,2,3", line.Record.ToString());

        Assert.True(parser.TryReadLine(out line, isFinalBlock: false));
        Assert.Equal(0, line.RecordLength);

        Assert.True(parser.TryReadLine(out line, isFinalBlock: false));
        Assert.Equal("4,5,6", line.Record.ToString());

        Assert.False(parser.TryReadLine(out _, isFinalBlock: false));
        Assert.False(parser.TryReadLine(out _, isFinalBlock: true));
    }
}
