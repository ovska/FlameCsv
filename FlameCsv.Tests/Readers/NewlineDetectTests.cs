﻿using System.Buffers;
using System.Text;
using FlameCsv.Exceptions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public static class NewlineDetectTests
{
    private static ReadOnlyMemory<byte> U8(string input) => Encoding.UTF8.GetBytes(input);

    [Theory]
    [InlineData("A,B,C\r\nX,Y,Z\r\n", false)]
    [InlineData("A,B,C\nX,Y,Z\n", false)]
    [InlineData("\"A\",B,C\nX,Y,Z\n", false)]
    [InlineData("A,B,C\r\n\"X\",Y,Z\r\n", false)]
    [InlineData("\"A\",B,C\r\n\"X\",Y,Z\r\n", false)]
    [InlineData("A,B,C\n\"X\",Y,Z\n", false)]
    [InlineData("A,B,C\r\n\"\"\"X\",Y,Z\r\n", false)]
    [InlineData("A,B,C\n\"\"\"X\",Y,Z\n", false)]
    [InlineData("A,B,C\r\nX,Y,Z\r\n", true)]
    [InlineData("A,B,C\nX,Y,Z\n", true)]
    [InlineData("A,\"^B\",C\r\nX,Y,Z\r\n", true)]
    [InlineData("A,\"^B\",C\nX,Y,Z\n", true)]
    [InlineData("A,B,C\r\n\"X\",Y,Z\r\n", true)]
    [InlineData("A,B,C\n\"X\",Y,Z\n", true)]
    [InlineData("A,B,C\r\n\"^\"X\",Y,Z\r\n", true)]
    [InlineData("A,B,C\n\"^\"X\",Y,Z\n", true)]
    public static void Should_Detect(string input, bool escape)
    {
        var textOpts = new CsvOptions<char> { Newline = null, Escape = escape ? '^' : null };
        var utf8Opts = new CsvOptions<byte> { Newline = null, Escape = escape ? '^' : null };

        // repeat to ensure the newline detection is valid for the parser instance, not whole Options
        for (int i = 0; i < 2; i++)
        {
            RunAssertions(textOpts, input.AsMemory());
            RunAssertions(utf8Opts, U8(input));
        }

        Assert.Null(textOpts.Newline);
        Assert.Null(utf8Opts.Newline);

        static void RunAssertions<T>(
            CsvOptions<T> options,
            ReadOnlyMemory<T> input) where T : unmanaged, IBinaryInteger<T>
        {
            foreach (var reader in CsvParser.Create(options, new ReadOnlySequence<T>(input)).ParseRecords())
            {
                Assert.Equal([T.CreateChecked('A')], reader[0]);
                Assert.Equal([T.CreateChecked('B')], reader[1]);
                Assert.Equal([T.CreateChecked('C')], reader[2]);
                break;
            }
        }
    }

    [Theory, MemberData(nameof(NewlineData))]
    public static void Should_Throw_On_Very_Long_Inputs(bool segments, NewlineToken? newline, bool shouldThrow)
    {
        string newlineStr = newline switch
        {
            NewlineToken.CRLF => "\r\n",
            NewlineToken.LF => "\n",
            _ => ""
        };

        var data = MemorySegment<char>.AsSequence(
            (new string('x', 4096) + newlineStr).AsMemory(),
            bufferSize: segments ? 2048 : -1);


        var ex = Record.Exception(() =>
        {
            foreach (var _ in CsvParser.Create(new CsvOptions<char> { Newline = null }, in data).ParseRecords())
            {

            }
        });

        if (shouldThrow)
        {
            Assert.IsType<CsvFormatException>(ex);
        }
        else
        {
            Assert.Null(ex);
        }
    }

    public static TheoryData<bool, NewlineToken?, bool> NewlineData
        => new()
        {
            { false, NewlineToken.CRLF, false },
            { false, NewlineToken.LF, false },
            { false, null, true },
            { true, NewlineToken.CRLF, true },
            { true, NewlineToken.LF, true },
            { true, null, true },
        };
}
