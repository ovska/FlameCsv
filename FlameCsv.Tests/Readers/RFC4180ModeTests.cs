using System.Buffers;
using System.Diagnostics;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public class RFC4180ModeTests
{
    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test", "test")]
    [InlineData("test ", "test")]
    [InlineData(" test ", "test")]
    [InlineData("\" test\"", " test")]
    [InlineData("\"test \"", "test ")]
    [InlineData("\" test \"", " test ")]
    public void Should_Trim_Fields(string input, string expected)
    {
        var result = input.Read(new CsvOptions<char> { Whitespace = " " });
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
        Assert.Equal([["field1", "field2", expected]], result);
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

        var result = hugefield.Read(new CsvOptions<char> { Newline = "\r\n" });
        Assert.Single(result);
        Assert.Equal([pt1 + '"' + pt2], result[0]);
    }

    [Fact]
    public void Should_Trim_LF_From_Next_Segment()
    {
        var data = MemorySegment.Create("record1\r", "\nrecord2");

        using var parser = new TestParser(CsvOptions<char>.Default, CsvPipeReader.Create(data), default);
        Assert.True(parser.TryAdvanceReader());

        Assert.True(parser.TryReadLine(out var fields, false));
        Assert.Equal("record1", fields.Record.ToString());

        Assert.True(parser.TryReadLine(out fields, false));
        Assert.Equal("record2", fields.Record.ToString());
    }

    [Fact]
    public void Should_Trim_LF_From_Next_Read_Result()
    {
        using var parser = new TestParser(CsvOptions<char>.Default, new FakeReader(), default);
        Assert.True(parser.TryAdvanceReader());

        Assert.True(parser.TryReadLine(out var fields, false));
        Assert.Equal("record1", fields.Record.ToString());

        Assert.False(parser.TryReadLine(out _, false));
        Assert.True(parser.TryAdvanceReader());

        Assert.True(parser.TryReadLine(out fields, false));
        Assert.Equal("record2", fields.Record.ToString());

        Assert.False(parser.TryAdvanceReader());
    }

    private sealed class TestParser : CsvParser<char>
    {
        internal TestParser(
            CsvOptions<char> options,
            ICsvPipeReader<char> reader,
            in CsvParserOptions<char> parserOptions)
            : base(options, reader, in parserOptions)
        {
        }

        private protected override bool TryReadFromSequence(out CsvFields<char> fields, bool isFinalBlock)
        {
            if (_sequence.IsEmpty)
            {
                fields = default;
                return false;
            }

            fields = new(this, _sequence.First, (Meta[]) [Meta.StartOfData, Meta.Plain((int)_sequence.Length, true, 0)]);
            _sequence = default;
            return true;
        }

        private protected override int ReadFromSpan(ReadOnlySpan<char> data)
        {
            int index = data.IndexOf('\r');
            if (index == -1) return 0;
            MetaBuffer[0] = Meta.Plain(index, isEOL: true, 1);
            return 1;
        }
    }

    private sealed class FakeReader : ICsvPipeReader<char>
    {
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => default;

        private int _counter;

        public CsvReadResult<char> Read()
        {
            if (_counter == 0) return new(new ReadOnlySequence<char>("record1\r".AsMemory()), false);
            if (_counter == 1) return new(new ReadOnlySequence<char>("\nrecord2".AsMemory()), true);
            throw new UnreachableException();
        }

        public ValueTask<CsvReadResult<char>> ReadAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<CsvReadResult<char>>(new NotSupportedException());
        }

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            if (consumed.GetInteger() == 0 && examined.GetInteger() == 0)
            {
                return;
            }

            if (_counter < 2)
            {
                _counter++;
                return;
            }

            throw new UnreachableException();
        }

        public bool TryReset() => throw new NotSupportedException();
    }
}

file static class Extensions
{
    public static List<List<string>> Read(in this ReadOnlySequence<char> input, CsvOptions<char> options)
    {
        List<List<string>> records = [];

        foreach (var reader in CsvParser.Create(options, in input).ParseRecords())
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
