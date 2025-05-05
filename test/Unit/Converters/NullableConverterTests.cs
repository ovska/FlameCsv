using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Converters;

public static class NullableConverterTests
{
    [Fact]
    public static void Should_Return_Null()
    {
        NullableConverter<char, int> converter = new(
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
            Converters = { factory, new SpanTextConverter<int>(CsvOptions<char>.Default) }, Null = "null",
        };
        var parser = (CsvConverter<char, int?>)factory.Create(typeof(int?), emptyOptions);
        Assert.True(parser.TryParse("null", out var value));
        Assert.Null(value);
    }

#if false
    [Fact]
    public static void Should_Use_Interface_Converter()
    {
        var options = new CsvOptions<char> { Converters = { new InterfaceConverter() } };
        Assert.True(NullableConverterFactory<char>.Instance.CanConvert(typeof(Shim?)));
        var converter = NullableConverterFactory<char>.Instance.Create(typeof(Shim?), options);

        var c = (CsvConverter<char, Shim?>)converter;

        Assert.True(c.TryParse("true", out var result));
        Assert.True(result.GetValueOrDefault().IsReadOnly);

        char[] buffer = new char[4];
        Assert.True(c.TryFormat(buffer, new Shim(true), out var written));
        Assert.Equal("True", buffer.AsSpan(..written).ToString());
    }
#endif

    private readonly record struct Shim(bool IsReadOnly) : ICanBeReadOnly;

    private sealed class InterfaceConverter : CsvConverter<char, ICanBeReadOnly>
    {
        public override bool CanConvert(Type type) => type.IsAssignableTo(typeof(ICanBeReadOnly));

        public override bool TryFormat(Span<char> destination, ICanBeReadOnly value, out int charsWritten)
        {
            return ((Shim)value).IsReadOnly.TryFormat(destination, out charsWritten);
        }

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out ICanBeReadOnly value)
        {
            if (bool.TryParse(source, out var v))
            {
                value = new Shim(v);
                return true;
            }

            value = null;
            return false;
        }
    }
}

file class FakeMemoryManager(int length) : MemoryManager<char>
{
    protected override void Dispose(bool disposing)
    {
    }

    public override Span<char> GetSpan() => new char[length];

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        throw new NotSupportedException();
    }

    public override void Unpin()
    {
        throw new NotSupportedException();
    }
}
