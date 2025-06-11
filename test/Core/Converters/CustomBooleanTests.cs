namespace FlameCsv.Tests.Converters;

public static class CustomBooleanTests
{
    [Fact]
    public static void Should_Convert_Utf16()
    {
        var options = new CsvOptions<char> { BooleanValues = { ("Yes", true), ("No", false) } };
        var converter = options.GetConverter<bool>();

        Assert.True(converter.TryParse("Yes", out var result));
        Assert.True(result);
        Assert.True(converter.TryParse("No", out result));
        Assert.False(result);
        Assert.False(converter.TryParse("Maybe", out result));

        char[] buffer = new char[10];
        Assert.True(converter.TryFormat(buffer, true, out var charsWritten));
        Assert.Equal(3, charsWritten);
        Assert.Equal("Yes", new string(buffer, 0, charsWritten));
        Assert.True(converter.TryFormat(buffer, false, out charsWritten));
        Assert.Equal(2, charsWritten);
        Assert.Equal("No", new string(buffer, 0, charsWritten));
        Assert.False(converter.TryFormat(buffer.AsSpan(0, 1), true, out charsWritten));
        Assert.False(converter.TryFormat(buffer.AsSpan(0, 1), false, out charsWritten));
    }

    [Fact]
    public static void Should_Convert_Utf8()
    {
        var options = new CsvOptions<byte> { BooleanValues = { ("Yes", true), ("No", false) } };
        var converter = options.GetConverter<bool>();

        Assert.True(converter.TryParse("Yes"u8, out var result));
        Assert.True(result);
        Assert.True(converter.TryParse("No"u8, out result));
        Assert.False(result);
        Assert.False(converter.TryParse("Maybe"u8, out result));

        byte[] buffer = new byte[10];
        Assert.True(converter.TryFormat(buffer, true, out var bytesWritten));
        Assert.Equal(3, bytesWritten);
        Assert.Equal("Yes"u8, buffer.AsSpan(0, bytesWritten));
        Assert.True(converter.TryFormat(buffer, false, out bytesWritten));
        Assert.Equal(2, bytesWritten);
        Assert.Equal("No"u8, buffer.AsSpan(0, bytesWritten));
        Assert.False(converter.TryFormat(buffer.AsSpan(0, 1), true, out bytesWritten));
        Assert.False(converter.TryFormat(buffer.AsSpan(0, 1), false, out bytesWritten));
    }
}
