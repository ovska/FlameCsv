using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Converters;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public static class RFC4180ModeTests
{
    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test", "test")]
    [InlineData("test ", "test")]
    [InlineData(" test ", "test")]
    [InlineData("\" test\"", "test")]
    [InlineData("\"test \"", "test")]
    [InlineData("\" test \"", "test")]
    public static void Should_Trim_Fields(string input, string expected)
    {
        var context = new CsvReadingContext<char>(new CsvTextOptions { Whitespace = " " });

        char[]? buffer = null;

        var reader = new CsvFieldReader<char>(
            input.AsMemory(),
            in context,
            stackalloc char[16],
            ref buffer,
            context.GetRecordMeta(input.AsMemory()).quoteCount);

        Assert.True(reader.TryReadNext(out ReadOnlySpan<char> field));
        Assert.Equal(expected, field.ToString());
        Assert.True(reader.End);
    }

    [Fact]
    public static void Should_Seek_Long_Line()
    {
        string input = "\"Long line with lots of content, but no quotes except the wrapping!\"";
        var context = new CsvReadingContext<char>(CsvTextOptions.Default);

        var reader = new CsvFieldReader<char>(
            input.AsMemory(),
            in context,
            [],
            ref Unsafe.NullRef<char[]?>(),
            context.GetRecordMeta(input.AsMemory()).quoteCount);

        Assert.True(reader.TryReadNext(out ReadOnlySpan<char> field));
        Assert.Equal(input[1..^1], field.ToString());
        Assert.True(reader.End);
    }

    [Fact]
    public static void Should_Unescape2()
    {
        Span<byte> test = stackalloc byte[Vector64<byte>.Count];
        test[7] = 123;
        var bytes = Vector64.Create<byte>(test);
        var mask = Vector64.Create<byte>(123);

        var equals = Vector64.Equals(bytes, mask);
        var lds = equals.ExtractMostSignificantBits();
        var charpos = uint.TrailingZeroCount(lds);

        var input = "The quick brown ''fox'' jumped over the lazy ''dog''.";
        Span<char> buffer = stackalloc char[128];

        RFC4180Mode<short>.Unescape(
            (short)'\'',
            buffer.Cast<char, short>().Slice(0, input.Replace("''", "'").Length),
            input.AsSpan().Cast<char, short>(),
            (uint)input.Count('\''));

        var expected = "The quick brown 'fox' jumped over the lazy 'dog'.";
        Assert.Equal(expected, buffer[..expected.Length].ToString());
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
    public static void Should_Unescape(string input, string expected)
    {
        var context = new CsvReadingContext<char>(CsvTextOptions.Default);

        char[]? unescapeArray = null;

        var record = new CsvFieldReader<char>(
            input.AsMemory(),
            in context,
            stackalloc char[128],
            ref unescapeArray,
            (uint)input.Count(c => c == '"'));

        var field = RFC4180Mode<char>.ReadNextField(ref record);

        Assert.Equal(expected, field.ToString());
    }

    [Theory]
    [InlineData(",,,,")]
    [InlineData("a,b,c,d,e")]
    [InlineData("x,y,asdalksdjasd,,")]
    [InlineData(",jklsadklasdW,laskdjlksad,,1231")]
    [InlineData("A,\"B\",C,D,E")]
    [InlineData("A,\"B\",C,D,\"E\"")]
    public static void Should_Enumerate_Fields(string line)
    {
        using var pool = new ReturnTrackingArrayPool<char>();
        var options = new CsvTextOptions
        {
            Newline = "|",
            ArrayPool = pool,
            AllowContentInExceptions = true,
        };

        var expected = line.Split(',').Select(s => s.Trim('"'));

        var list = new List<string>();
        var context = new CsvReadingContext<char>(options);

        char[]? buffer = null;

        CsvFieldReader<char> reader = new(
            line.AsMemory(),
            in context,
            [],
            ref buffer,
            context.GetRecordMeta(line.AsMemory()).quoteCount);

        while (!reader.End)
        {
            list.Add(RFC4180Mode<char>.ReadNextField(ref reader).ToString());
        }

        Assert.Equal(expected, list);
        context.ArrayPool.EnsureReturned(ref buffer);
    }

    [Fact]
    public static void Should_Enumerate_With_Comma2()
    {
        using var pool = new ReturnTrackingArrayPool<char>();
        var options = new CsvTextOptions
        {
            Newline = "|",
            ArrayPool = pool,
            AllowContentInExceptions = true,
        };

        var data = new[] { options.Delimiter, options.Newline[0] }.GetPermutations();
        char[]? buffer = null;

        var context = new CsvReadingContext<char>(options);

        foreach (var chars in data)
        {
            var input = new string(chars.ToArray());
            var line = $"\"{input}\",test";

            CsvFieldReader<char> state = new(
                line.AsMemory(),
                in context,
                [],
                ref buffer,
                (uint)line.Count('"'));

            var list = new List<string>();

            while (!state.End)
            {
                list.Add(RFC4180Mode<char>.ReadNextField(ref state).ToString());
            }

            Assert.Equal(2, list.Count);
            Assert.Equal(input, list[0]);
            Assert.Equal("test", list[1]);
        }

        context.ArrayPool.EnsureReturned(ref buffer);
    }
}
