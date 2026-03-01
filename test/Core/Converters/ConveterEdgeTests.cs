using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public partial class ConverterEdgeTests
{
    [Fact]
    public void Should_Handle_Null_String_Format()
    {
        Assert.True(StringTextConverter.Instance.TryFormat(Span<char>.Empty, null!, out int charsWritten));
        Assert.Equal(0, charsWritten);

        Assert.True(StringUtf8Converter.Instance.TryFormat(Span<byte>.Empty, null!, out charsWritten));
        Assert.Equal(0, charsWritten);

        Assert.True(CsvPoolingStringTextConverter.Instance.TryFormat(Span<char>.Empty, null!, out charsWritten));
        Assert.Equal(0, charsWritten);

        Assert.True(CsvPoolingStringUtf8Converter.Instance.TryFormat(Span<byte>.Empty, null!, out charsWritten));
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void Should_Handle_Too_Small_Destination()
    {
        Assert.False(StringTextConverter.Instance.TryFormat(Span<char>.Empty, "foo", out _));
        Assert.False(StringUtf8Converter.Instance.TryFormat(Span<byte>.Empty, "foo", out _));
        Assert.False(CsvPoolingStringTextConverter.Instance.TryFormat(Span<char>.Empty, "foo", out _));
        Assert.False(CsvPoolingStringUtf8Converter.Instance.TryFormat(Span<byte>.Empty, "foo", out _));
    }
}
