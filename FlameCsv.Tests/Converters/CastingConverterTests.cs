using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Converters;

public static class CastingConverterTests
{
    private class Base
    {
    }

    private class Derived : Base
    {
    }

    private class Obj
    {
        [CsvConverter<char, DerivedConverter>]
        public Base? Value { get; set; }
    }

    private abstract class SomeConverter<TValue> : CsvConverter<char, TValue> where TValue : new()
    {
        protected abstract ReadOnlySpan<char> Span { get; }

        public override bool TryFormat(Span<char> destination, TValue value, out int charsWritten)
        {
            return Span.TryWriteTo(destination, out charsWritten);
        }

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out TValue value)
        {
            if (source.SequenceEqual(Span))
            {
                value = new();
                return true;
            }

            value = default;
            return false;
        }
    }

    private sealed class BaseConverter : SomeConverter<Base>
    {
        protected override ReadOnlySpan<char> Span => nameof(Base);
    }

    private sealed class DerivedConverter : SomeConverter<Derived>
    {
        protected override ReadOnlySpan<char> Span => nameof(Derived);
    }

    [Fact]
    public static void Should_Convert()
    {
        var converter = new CastingConverter<char, Derived, Base>(
            new DerivedConverter(),
            "null".AsMemory());

        Assert.True(converter.TryParse("Derived", out Base? value));
        Assert.NotNull(value);

        var buffer = new char[32];

        converter.TryFormat(buffer, value!, out int charsWritten);
        Assert.Equal("Derived", new string(buffer, 0, charsWritten));

        converter.TryFormat(buffer, null!, out charsWritten);
        Assert.Equal("null", new string(buffer, 0, charsWritten));
    }

    [Fact]
    public static void Should_Create_Casting()
    {
        var converter = new CsvConverterAttribute<char, DerivedConverter>().CreateConverter(typeof(Base), CsvTextOptions.Default);
        Assert.IsType<CastingConverter<char, Derived, Base>>(converter);
    }
}
