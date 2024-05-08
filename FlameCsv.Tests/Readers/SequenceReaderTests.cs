using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public class SequenceReaderTests
{
    private readonly CsvReadingContext<char> _crlfContext;
    private readonly CsvReadingContext<char> _lfContext;
    private char[]? _array;

    private LineSeekArg<char> LFArg()
    {
        return new(in _lfContext, ref _array);
    }

    private LineSeekArg<char> CRLFArg()
    {
        return new(in _crlfContext, ref _array);
    }

    public SequenceReaderTests()
    {
        _crlfContext = new(new CsvTextOptions { Newline = "\r\n", ArrayPool = AllocatingArrayPool<char>.Shared });
        _lfContext = new(new CsvTextOptions { Newline = "\n", ArrayPool = AllocatingArrayPool<char>.Shared });
    }

    [Fact]
    public void Should_Read_LF()
    {
        var data = "1,Alice,true\n2,Bob,false\n";

        var reader = new CsvSequenceReader<char>(MemorySegment<char>.AsSequence(data.AsMemory(), 64));
        var arg = LFArg();

        Assert.True(reader.TryReadLine(arg, out var line, out RecordMeta meta));
        Assert.Equal("1,Alice,true", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.True(reader.TryReadLine(arg, out line, out meta));
        Assert.Equal("2,Bob,false", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.False(reader.TryReadLine(arg, out _, out _));
    }

    [Fact]
    public void Should_Read_CRLF()
    {
        var data = "1,Alice,true\r\n2,Bob,false\r\n";

        var reader = new CsvSequenceReader<char>(MemorySegment<char>.AsSequence(data.AsMemory(), 64));
        var arg = CRLFArg();

        Assert.True(reader.TryReadLine(arg, out var line, out RecordMeta meta));
        Assert.Equal("1,Alice,true", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.True(reader.TryReadLine(arg, out line, out meta));
        Assert.Equal("2,Bob,false", line.ToString());
        Assert.Equal(0u, meta.quoteCount);
        Assert.False(reader.TryReadLine(arg, out _, out _));
    }

    [Fact]
    public void Should_Find_Multisegment_Newlines()
    {
        const string start = "xyz\r";
        const string end = "\nabc";
        var first = new MemorySegment<char>(start.AsMemory());
        var last = first.Append(end.AsMemory());

        var seq = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
        var reader = new CsvSequenceReader<char>(seq);
        var arg = CRLFArg();

        Assert.True(reader.TryReadLine(arg, out var line, out _));

        Assert.Equal("xyz", line.ToString());
        Assert.Equal("abc", reader.UnreadSequence.ToString());
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

        var reader = new CsvSequenceReader<char>(seq);
        var arg = CRLFArg();

        Assert.True(reader.TryReadLine(arg, out var line, out var meta));
        Assert.Equal(s1, line.ToString());
        Assert.Equal(2u, meta.quoteCount);

        Assert.False(reader.TryReadLine(arg, out _, out _));
        Assert.Equal(s3 + s4, reader.UnreadSequence.ToString());
    }

    private static readonly string[] _xxlines = [.. Enumerable.Range(0, 128).Select(i => new string('x', i))];

    public static IEnumerable<object[]> Segments =>
        from size in (int[])[1, 2, 4, 17, 128, 8096]
        from freq in (int[])[0, 2, 20]
        select (object[])[size, freq];

    [Theory, MemberData(nameof(Segments))]
    public void Should_Find_Complex_Newlines(int segmentSize, int emptyFrequency)
    {
        var joined = string.Join("\r\n", _xxlines);
        var reader = new CsvSequenceReader<char>(
            MemorySegment<char>.AsSequence(joined.AsMemory(), segmentSize, emptyFrequency));

        var arg = CRLFArg();

        var results = new List<string>();

        while (reader.TryReadLine(arg, out var line, out _))
        {
            results.Add(line.ToString());
        }

        if (!reader.End)
            results.Add(reader.UnreadSequence.ToString());

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
        var reader = new CsvSequenceReader<char>(seq);
        var arg = crlf ? CRLFArg() : LFArg();

        Assert.True(reader.TryReadLine(arg, out var line, out _));
        Assert.Equal(segments[0], line.ToString());
        Assert.Equal(segments[2], reader.UnreadSequence.ToString());
    }

    [Fact]
    public void Should_Handle_Line_With_Uneven_Quotes_No_Newline()
    {
        const string data = "\"testxyz\",\"broken";
        var seq = new ReadOnlySequence<char>(data.AsMemory());
        var reader = new CsvSequenceReader<char>(seq);
        var arg1 = CRLFArg();
        var arg2 = LFArg();

        Assert.False(reader.TryReadLine(arg1, out _, out _));
        Assert.Equal(data, reader.UnreadSequence.ToString());

        Assert.False(reader.TryReadLine(arg2, out _, out _));
        Assert.Equal(data, reader.UnreadSequence.ToString());
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
        var reader = new CsvSequenceReader<char>(seq);
        var context = new CsvReadingContext<char>(new CsvTextOptions { Newline = "|", ArrayPool = ReturnTrackingArrayPool<char>.Shared });
        var arg = new LineSeekArg<char>(in context, ref _array);

        bool result = reader.TryReadLine(arg, out var line, out var meta);

        if (data.Contains('|'))
        {
            Assert.True(result);
            var lineStr = line.ToString();
            Assert.Equal(expected, lineStr);
            Assert.Equal(data[(lineStr.Length + 1)..], reader.UnreadSequence.ToString());
            Assert.Equal(quoteCount, meta.quoteCount);
        }
        else
        {
            Assert.False(result);

            // original sequence is unchanged
            Assert.Equal(data, reader.Unread.ToString());
        }
    }
}
