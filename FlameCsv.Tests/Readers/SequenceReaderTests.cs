﻿using System.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public class SequenceReaderTests
{
    private readonly CsvTextOptions _crlfOptions = new() { Newline = "\r\n", ArrayPool = new ReturnTrackingArrayPool<char>() };
    private readonly CsvTextOptions _lfOptions = new() { Newline = "\n", ArrayPool = new ReturnTrackingArrayPool<char>() };

    [Fact]
    public void Should_Read_LF()
    {
        var data = "1,Alice,true\n2,Bob,false\n";

        using var parser = CsvParser<char>.Create(_lfOptions);
        parser.Reset(MemorySegment<char>.AsSequence(data.AsMemory(), 64));

        Assert.True(parser.TryReadLine(out var line, out CsvRecordMeta meta, isFinalBlock: false));
        Assert.Equal("1,Alice,true", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.True(parser.TryReadLine(out line, out meta, isFinalBlock: false));
        Assert.Equal("2,Bob,false", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.False(parser.TryReadLine(out _, out _, isFinalBlock: false));
    }

    [Fact]
    public void Should_Read_CRLF()
    {
        var data = "1,Alice,true\r\n2,Bob,false\r\n";

        using var parser = CsvParser<char>.Create(_crlfOptions);
        parser.Reset(MemorySegment<char>.AsSequence(data.AsMemory(), 64));

        Assert.True(parser.TryReadLine(out var line, out CsvRecordMeta meta, isFinalBlock: false));
        Assert.Equal("1,Alice,true", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.True(parser.TryReadLine(out line, out meta, isFinalBlock: false));
        Assert.Equal("2,Bob,false", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.False(parser.TryReadLine(out _, out _, isFinalBlock: false));
    }

    [Fact]
    public void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.AsMemory());
        var last = first.Append(end.AsMemory());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        using var parser = CsvParser<char>.Create(_crlfOptions);
        parser.Reset(in seq);

        Assert.True(parser.TryReadLine(out var line, out _, isFinalBlock: false));

        Assert.Equal("xyz", line.ToString());
        Assert.Equal("abc", parser._reader.UnreadSequence.ToString());
    }

    [Fact]
    public void Should_Handle_Segment_Ending_Quote()
    {
        const string s1 = "\"test\"";
        const string s2 = "\r\n";
        const string s3 = "\"te";
        const string s4 = /**/"st2\"";
        var first = new MemorySegment<char>(s1.AsMemory());
        var last = first.Append(s2.AsMemory()).Append(s3.AsMemory()).Append(s4.AsMemory());
        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        using var parser = CsvParser<char>.Create(_crlfOptions);
        parser.Reset(in seq);

        Assert.True(parser.TryReadLine(out var line, out var meta, isFinalBlock: false));
        Assert.Equal(s1, line.ToString());
        Assert.Equal(2u, meta.quoteCount);

        Assert.False(parser.TryReadLine(out _, out _, isFinalBlock: false));
        Assert.Equal(s3 + s4, parser._reader.UnreadSequence.ToString());
    }

    private static readonly string[] _xxlines = [.. Enumerable.Range(0, 128).Select(i => new string('x', i))];

    public static TheoryData<int, int> Segments
    {
        get
        {
            var values = from size in (int[])[1, 2, 4, 17, 128, 8096]
                         from freq in (int[])[0, 2, 20]
                         select new { size, freq };
            var data = new TheoryData<int, int>();

            foreach (var x in values)
            {
                data.Add(x.size, x.freq);
            }

            return data;
        }
    }

    [Theory, MemberData(nameof(Segments))]
    public void Should_Find_Complex_Newlines(int segmentSize, int emptyFrequency)
    {
        var joined = string.Join("\r\n", _xxlines);
        using var parser = CsvParser<char>.Create(_crlfOptions);
        parser.Reset(MemorySegment<char>.AsSequence(joined.AsMemory(), segmentSize, emptyFrequency));

        var results = new List<string>();

        while (parser.TryReadLine(out var line, out _, isFinalBlock: false))
        {
            results.Add(line.ToString());
        }

        if (!parser.End)
            results.Add(parser._reader.UnreadSequence.ToString());

        Assert.Equal(_xxlines, results);
    }

    [Theory]
    [InlineData(data: [true, new[] { "abc", "\r\n", "xyz" }])]
    [InlineData(data: [false, new[] { "abc", "\n", "xyz" }])]
    public void Should_Find_Segment_With_Only_Newline(bool crlf, string[] segments)
    {
        var first = new MemorySegment<char>(segments[0].AsMemory());
        var last = first
            .Append(segments[1].AsMemory())
            .Append(segments[2].AsMemory());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        var context = crlf ? _crlfOptions : _lfOptions;
        using var parser = CsvParser<char>.Create(context);
        parser.Reset(in seq);

        Assert.True(parser.TryReadLine(out var line, out _, isFinalBlock: false));
        Assert.Equal(segments[0], line.ToString());
        Assert.Equal(segments[2], parser._reader.UnreadSequence.ToString());
    }

    [Fact]
    public void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var seq = new ReadOnlySequence<char>(data.AsMemory());

        using var parser1 = CsvParser<char>.Create(_lfOptions);
        parser1.Reset(in seq);
        Assert.False(parser1.TryReadLine(out _, out _, isFinalBlock: false));
        Assert.Equal(data, parser1._reader.UnreadSequence.ToString());

        using var parser2 = CsvParser<char>.Create(_crlfOptions);
        parser2.Reset(in seq);
        Assert.False(parser2.TryReadLine(out _, out _, isFinalBlock: false));
        Assert.Equal(data, parser2._reader.UnreadSequence.ToString());
    }

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("|", "", 0)]
    [InlineData("a|", "a", 0)]
    [InlineData("xyz|xyz", "xyz", 0)]
    [InlineData("\"test\"|abc", "\"test\"", 2)]
    [InlineData("\"test|\"|def", "\"test|\"", 2)]
    [InlineData("\"test|\",\"test2|\"|xyz", "\"test|\",\"test2|\"", 4)]
    [InlineData("\"a|\",\"b|\",\"c|\"|XX", "\"a|\",\"b|\",\"c|\"", 6)]
    [InlineData("\"James \"\"007\"\" Bond\"|Agent", "\"James \"\"007\"\" Bond\"", 6)]
    public void Should_Find_Lines(string data, string expected, uint quoteCount)
    {
        var seq = new ReadOnlySequence<char>(data.AsMemory());
        using var parser = CsvParser<char>.Create(new CsvTextOptions { Newline = "|", ArrayPool = ReturnTrackingArrayPool<char>.Shared });
        parser.Reset(seq);

        bool result = parser.TryReadLine(out var line, out var meta, isFinalBlock: false);

        if (data.Contains('|'))
        {
            Assert.True(result);
            var lineStr = line.ToString();
            Assert.Equal(expected, lineStr);
            Assert.Equal(data[(lineStr.Length + 1)..], parser._reader.UnreadSequence.ToString());
            Assert.Equal(quoteCount, meta.quoteCount);
        }
        else
        {
            Assert.False(result);

            // original sequence is unchanged
            Assert.Equal(data, parser._reader.UnreadSequence.ToString());
        }
    }
}
