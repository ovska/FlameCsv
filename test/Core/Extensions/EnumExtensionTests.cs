using FlameCsv.Converters.Enums;

namespace FlameCsv.Tests.Extensions;

public static class EnumExtensionTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData("9", true)]
    [InlineData("-1", false)] // ByteEnum doesn't support negative values
    [InlineData("a", false)]
    public static void CanParseNumber_ByteEnum_Works(string input, bool expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool actual = EnumExtensions.CanParseNumber<char, ByteEnum>(source);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData("9", true)]
    [InlineData("-1", true)] // SByteEnum supports negative values
    [InlineData("a", false)]
    public static void CanParseNumber_SByteEnum_Works(string input, bool expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool actual = EnumExtensions.CanParseNumber<char, SByteEnum>(source);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", ByteEnum.One)]
    [InlineData("2", ByteEnum.Two)]
    [InlineData("4", ByteEnum.Four)]
    public static void TryParseNumber_ByteEnum_WithChar_ParsesCorrectValue(string input, ByteEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out ByteEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", ByteEnum.One)]
    [InlineData("2", ByteEnum.Two)]
    [InlineData("4", ByteEnum.Four)]
    public static void TryParseNumber_ByteEnum_WithBytes_ParsesCorrectValue(string input, ByteEnum expected)
    {
        ReadOnlySpan<byte> source = input.AsSpan().ToArray().Select(c => (byte)c).ToArray();
        bool success = EnumExtensions.TryParseNumber(source, out ByteEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("-1", SByteEnum.Negative)]
    [InlineData("1", SByteEnum.One)]
    [InlineData("2", SByteEnum.Two)]
    [InlineData("4", SByteEnum.Four)]
    public static void TryParseNumber_SByteEnum_WithChar_ParsesCorrectValue(string input, SByteEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out SByteEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("-1", ShortEnum.Negative)]
    [InlineData("1", ShortEnum.One)]
    [InlineData("2", ShortEnum.Two)]
    [InlineData("4", ShortEnum.Four)]
    public static void TryParseNumber_ShortEnum_WithChar_ParsesCorrectValue(string input, ShortEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out ShortEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", UShortEnum.One)]
    [InlineData("2", UShortEnum.Two)]
    [InlineData("4", UShortEnum.Four)]
    public static void TryParseNumber_UShortEnum_WithChar_ParsesCorrectValue(string input, UShortEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out UShortEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("-1", IntEnum.Negative)]
    [InlineData("1", IntEnum.One)]
    [InlineData("2", IntEnum.Two)]
    [InlineData("4", IntEnum.Four)]
    public static void TryParseNumber_IntEnum_WithChar_ParsesCorrectValue(string input, IntEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out IntEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", UIntEnum.One)]
    [InlineData("2", UIntEnum.Two)]
    [InlineData("4", UIntEnum.Four)]
    public static void TryParseNumber_UIntEnum_WithChar_ParsesCorrectValue(string input, UIntEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out UIntEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("-1", LongEnum.Negative)]
    [InlineData("1", LongEnum.One)]
    [InlineData("2", LongEnum.Two)]
    [InlineData("4", LongEnum.Four)]
    public static void TryParseNumber_LongEnum_WithChar_ParsesCorrectValue(string input, LongEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out LongEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", ULongEnum.One)]
    [InlineData("2", ULongEnum.Two)]
    [InlineData("4", ULongEnum.Four)]
    public static void TryParseNumber_ULongEnum_WithChar_ParsesCorrectValue(string input, ULongEnum expected)
    {
        ReadOnlySpan<char> source = input.AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out ULongEnum actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void AddFlag_ByteEnum_AddsCorrectFlags()
    {
        var value = ByteEnum.One;
        value.AddFlag(ByteEnum.Two);
        Assert.Equal(ByteEnum.One | ByteEnum.Two, value);
    }

    [Fact]
    public static void AddFlag_SByteEnum_AddsCorrectFlags()
    {
        var value = SByteEnum.One;
        value.AddFlag(SByteEnum.Two);
        Assert.Equal(SByteEnum.One | SByteEnum.Two, value);
    }

    [Fact]
    public static void AddFlag_ShortEnum_AddsCorrectFlags()
    {
        var value = ShortEnum.One;
        value.AddFlag(ShortEnum.Two);
        Assert.Equal(ShortEnum.One | ShortEnum.Two, value);
    }

    [Fact]
    public static void AddFlag_UShortEnum_AddsCorrectFlags()
    {
        var value = UShortEnum.One;
        value.AddFlag(UShortEnum.Two);
        Assert.Equal(UShortEnum.One | UShortEnum.Two, value);
    }

    [Fact]
    public static void AddFlag_IntEnum_AddsCorrectFlags()
    {
        var value = IntEnum.One;
        value.AddFlag(IntEnum.Two);
        Assert.Equal(IntEnum.One | IntEnum.Two, value);
    }

    [Fact]
    public static void AddFlag_UIntEnum_AddsCorrectFlags()
    {
        var value = UIntEnum.One;
        value.AddFlag(UIntEnum.Two);
        Assert.Equal(UIntEnum.One | UIntEnum.Two, value);
    }

    [Fact]
    public static void AddFlag_LongEnum_AddsCorrectFlags()
    {
        var value = LongEnum.One;
        value.AddFlag(LongEnum.Two);
        Assert.Equal(LongEnum.One | LongEnum.Two, value);
    }

    [Fact]
    public static void AddFlag_ULongEnum_AddsCorrectFlags()
    {
        var value = ULongEnum.One;
        value.AddFlag(ULongEnum.Two);
        Assert.Equal(ULongEnum.One | ULongEnum.Two, value);
    }

    [Fact]
    public static void ClearFlag_ByteEnum_ClearsCorrectFlags()
    {
        var value = ByteEnum.All;
        value.ClearFlag(ByteEnum.Two);
        Assert.Equal(ByteEnum.One | ByteEnum.Four, value);
    }

    [Fact]
    public static void ClearFlag_SByteEnum_ClearsCorrectFlags()
    {
        var value = SByteEnum.All;
        value.ClearFlag(SByteEnum.Two);
        Assert.Equal(SByteEnum.One | SByteEnum.Four, value);
    }

    [Fact]
    public static void ClearFlag_ShortEnum_ClearsCorrectFlags()
    {
        var value = ShortEnum.All;
        value.ClearFlag(ShortEnum.Two);
        Assert.Equal(ShortEnum.One | ShortEnum.Four, value);
    }

    [Fact]
    public static void ClearFlag_UShortEnum_ClearsCorrectFlags()
    {
        var value = UShortEnum.All;
        value.ClearFlag(UShortEnum.Two);
        Assert.Equal(UShortEnum.One | UShortEnum.Four, value);
    }

    [Fact]
    public static void ClearFlag_IntEnum_ClearsCorrectFlags()
    {
        var value = IntEnum.All;
        value.ClearFlag(IntEnum.Two);
        Assert.Equal(IntEnum.One | IntEnum.Four, value);
    }

    [Fact]
    public static void ClearFlag_UIntEnum_ClearsCorrectFlags()
    {
        var value = UIntEnum.All;
        value.ClearFlag(UIntEnum.Two);
        Assert.Equal(UIntEnum.One | UIntEnum.Four, value);
    }

    [Fact]
    public static void ClearFlag_LongEnum_ClearsCorrectFlags()
    {
        var value = LongEnum.All;
        value.ClearFlag(LongEnum.Two);
        Assert.Equal(LongEnum.One | LongEnum.Four, value);
    }

    [Fact]
    public static void ClearFlag_ULongEnum_ClearsCorrectFlags()
    {
        var value = ULongEnum.All;
        value.ClearFlag(ULongEnum.Two);
        Assert.Equal(ULongEnum.One | ULongEnum.Four, value);
    }

    [Fact]
    public static void ToBitmask_ByteEnum_ReturnsCorrectBitmask()
    {
        var value = ByteEnum.All;
        var result = value.ToBitmask();
        Assert.Equal(7UL, result);
    }

    [Fact]
    public static void ToBitmask_UShortEnum_ReturnsCorrectBitmask()
    {
        var value = UShortEnum.All;
        var result = value.ToBitmask();
        Assert.Equal(7UL, result);
    }

    [Fact]
    public static void ToBitmask_UIntEnum_ReturnsCorrectBitmask()
    {
        var value = UIntEnum.All;
        var result = value.ToBitmask();
        Assert.Equal(7UL, result);
    }

    [Fact]
    public static void ToBitmask_ULongEnum_ReturnsCorrectBitmask()
    {
        var value = ULongEnum.All;
        var result = value.ToBitmask();
        Assert.Equal(7UL, result);
    }

    [Fact]
    public static void TryParseNumber_WithInvalidValue_ReturnsFalse()
    {
        ReadOnlySpan<char> source = "abc".AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out ByteEnum _);
        Assert.False(success);
    }

    [Fact]
    public static void TryParseNumber_WithOutOfRangeValue_ReturnsFalse()
    {
        ReadOnlySpan<char> source = "256".AsSpan(); // Out of range for byte
        bool success = EnumExtensions.TryParseNumber(source, out ByteEnum _);
        Assert.False(success);
    }

    [Fact]
    public static void TryParseNumber_WithNegativeValueForUnsignedEnum_ReturnsFalse()
    {
        ReadOnlySpan<char> source = "-1".AsSpan();
        bool success = EnumExtensions.TryParseNumber(source, out ByteEnum _);
        Assert.False(success);
    }

    // ReSharper disable UnusedMember.Global
    [Flags]
    public enum ByteEnum : byte
    {
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }

    [Flags]
    public enum SByteEnum : sbyte
    {
        Negative = -1,
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }

    [Flags]
    public enum ShortEnum : short
    {
        Negative = -1,
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }

    [Flags]
    public enum UShortEnum : ushort
    {
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }

    [Flags]
    public enum IntEnum
    {
        Negative = -1,
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }

    [Flags]
    public enum UIntEnum : uint
    {
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }

    [Flags]
    public enum LongEnum : long
    {
        Negative = -1,
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }

    [Flags]
    public enum ULongEnum : ulong
    {
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        All = One | Two | Four,
    }
}
