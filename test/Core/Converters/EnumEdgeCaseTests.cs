using FlameCsv.Attributes;
using FlameCsv.Reflection;

namespace FlameCsv.Tests.Converters;

public partial class EnumEdgeCaseTests
{
    [CsvEnumConverter<char, FlagsNoZero>]
    private partial class FlagsNoZeroConverterChar;

    [CsvEnumConverter<char, FlagsWithZero>]
    private partial class FlagsWithZeroConverterChar;

    [CsvEnumConverter<byte, FlagsNoZero>]
    private partial class FlagsNoZeroConverterByte;

    [CsvEnumConverter<byte, FlagsWithZero>]
    private partial class FlagsWithZeroConverterByte;

    [Flags]
    private enum FlagsWithZero
    {
        One = 1,
        Two = 2,
        Zero = 0,
    }

    [Flags]
    private enum FlagsNoZero
    {
        One = 1,
        Two = 2,
    }

    [Fact]
    public void Should_Format_Zero()
    {
        Impl(new FlagsNoZeroConverterChar(CsvOptions<char>.Default), "0");
        Impl(new FlagsWithZeroConverterChar(CsvOptions<char>.Default), "Zero");
        Impl(new FlagsNoZeroConverterByte(CsvOptions<byte>.Default), "0");
        Impl(new FlagsWithZeroConverterByte(CsvOptions<byte>.Default), "Zero");

        static void Impl<T, TValue>(CsvConverter<T, TValue> converter, string expected)
            where T : unmanaged, IBinaryInteger<T>
        {
            Span<T> buffer = stackalloc T[16];
            Assert.True(converter.TryFormat(buffer, default!, out int charsWritten));
            Assert.Equal(expected, Transcode.ToString(buffer.Slice(0, charsWritten)));

            // value is cached, run test twice
            Assert.True(converter.TryFormat(buffer, default!, out charsWritten));
            Assert.Equal(expected, Transcode.ToString(buffer.Slice(0, charsWritten)));
        }
    }

    [Fact]
    public void Should_Fall_Back_To_TryFormat_For_Hex()
    {
        var c = new FlagsWithZeroConverterChar(new CsvOptions<char> { EnumFormat = "X" });

        Span<char> buffer = stackalloc char[16];
        Assert.True(c.TryFormat(buffer, FlagsWithZero.Two, out int charsWritten));
        Assert.Equal(FlagsWithZero.Two.ToString("X"), new string(buffer.Slice(0, charsWritten)));
    }

    [Fact]
    public void Should_Return_Format_Char()
    {
        Assert.Equal('g', EnumFormat.Get(null));
        Assert.Equal('g', EnumFormat.Get(""));
        Assert.Equal('g', EnumFormat.Get("g"));
        Assert.Equal('g', EnumFormat.Get("G"));
        Assert.Equal('d', EnumFormat.Get("d"));
        Assert.Equal('d', EnumFormat.Get("D"));
        Assert.Equal('x', EnumFormat.Get("x"));
        Assert.Equal('f', EnumFormat.Get("f"));
        Assert.Null(EnumFormat.Get("a"));
        Assert.Null(EnumFormat.Get("g "));
    }
}
