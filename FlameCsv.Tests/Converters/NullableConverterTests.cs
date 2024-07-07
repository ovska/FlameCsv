using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public static class NullableConverterTests
{
    [Fact]
    public static void Should_Return_Null()
    {
        var converter = new NullableConverter<char, int>(
            new SpanTextConverter<int>(CsvOptions<char>.Default),
            "".AsMemory());

        Assert.True(converter.TryParse("", out var value1));
        Assert.Null(value1);

        Assert.True(converter.TryParse("1", out var value2));
        Assert.Equal(1, value2);

        Assert.False(converter.TryParse(" ", out _));
    }

    [Fact]
    public static void Should_Create_Converter()
    {
        var factory = NullableConverterFactory<char>.Instance;

        Assert.True(factory.CanConvert(typeof(int?)));
        Assert.False(factory.CanConvert(typeof(int)));

        var emptyOptions = new CsvOptions<char>
        {
            Converters = { factory, new SpanTextConverter<int>(CsvOptions<char>.Default) },
            Null = "null",
        };
        var parser = (CsvConverter<char, int?>)factory.Create(typeof(int?), emptyOptions);
        Assert.True(parser.TryParse("null", out var value));
        Assert.Null(value);
    }

    [Fact(Skip = "Not yet implemented")]
    public static void Should_Use_Interface_Converter()
    {
        var options = new CsvOptions<char> { Converters = { new DisposableConverter() } };
        Assert.True(NullableConverterFactory<char>.Instance.CanConvert(typeof(Shim?)));
        var converter = NullableConverterFactory<char>.Instance.Create(typeof(Shim?), options);
    }

    private readonly record struct Shim(int Id) : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class DisposableConverter : CsvConverter<char, IDisposable>
    {
        public override bool TryFormat(Span<char> destination, IDisposable value, out int charsWritten)
        {
            return ((Shim)value).Id.TryFormat(destination, out charsWritten); ;
        }

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out IDisposable value)
        {
            if (int.TryParse(source, out int id))
            {
                value = new Shim(id);
                return true;
            }

            value = default;
            return false;
        }
    }
}
