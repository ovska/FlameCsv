using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Converters;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
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
        using var parser = CsvParser<char>.Create(new CsvTextOptions { Whitespace = " " });

        char[]? buffer = null;

        var reader = new CsvFieldReader<char>(
            parser.Options,
            input.AsMemory(),
            stackalloc char[16],
            ref buffer,
            parser.GetRecordMeta(input.AsMemory()).quoteCount);

        Assert.True(reader.TryReadNext(out ReadOnlySpan<char> field));
        Assert.Equal(expected, field.ToString());
        Assert.True(reader.End);
    }

    [Fact]
    public static void Should_Seek_Long_Line()
    {
        string input = "\"Long line with lots of content, but no quotes except the wrapping!\"";
        using var parser = CsvParser<char>.Create(CsvTextOptions.Default);

        var reader = new CsvFieldReader<char>(
            parser.Options,
            input.AsMemory(),
            [],
            ref Unsafe.NullRef<char[]?>(),
            parser.GetRecordMeta(input.AsMemory()).quoteCount);

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

        var eqeq = Vector64.Equals(Vector64<byte>.Zero,equals);
        var dot = Vector64.Dot(equals, Vector64<byte>.Zero);
        var dot2 = Vector64.Dot(equals, Vector64<byte>.One);
        var sum = Vector64.Sum(eqeq);

        Vector64<byte> partials = Vector64<byte>.Zero;
        var neg = -equals;
        var zum = Vector64.Sum(neg);

        var sup1 = SimdVector128<ushort>.IsValid;
        var sup2 = SimdVector256<ushort>.IsValid;
        var sup3 = SimdVector512<ushort>.IsValid;
        var sap1 = SimdVector128<byte>.IsValid;
        var sap2 = SimdVector256<byte>.IsValid;
        var sap3 = SimdVector512<byte>.IsValid;

        var xcnt = Vector<byte>.Count;
        var ycnt = Vector< ushort>.Count;

        var b64cnt = Vector64<byte>.Count;
        var b128cnt = Vector128<byte>.Count;
        var b256cnt = Vector256<byte>.Count;

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
        using var parser = CsvParser<char>.Create(CsvTextOptions.Default);

        char[]? unescapeArray = null;

        var record = new CsvFieldReader<char>(
            parser.Options,
            input.AsMemory(),
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
        using var parser = CsvParser<char>.Create(options);

        char[]? buffer = null;

        CsvFieldReader<char> reader = new(
            parser.Options,
            line.AsMemory(),
            [],
            ref buffer,
            parser.GetRecordMeta(line.AsMemory()).quoteCount);

        while (!reader.End)
        {
            list.Add(RFC4180Mode<char>.ReadNextField(ref reader).ToString());
        }

        Assert.Equal(expected, list);
        options.ArrayPool.EnsureReturned(ref buffer);
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

        using var parser = CsvParser<char>.Create(options);

        foreach (var chars in data)
        {
            var input = new string(chars.ToArray());
            var line = $"\"{input}\",test";

            CsvFieldReader<char> state = new(
                parser.Options,
                line.AsMemory(),
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

        options.ArrayPool.EnsureReturned(ref buffer);
    }
}
