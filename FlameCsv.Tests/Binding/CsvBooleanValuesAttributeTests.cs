using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Converters;

// ReSharper disable UnusedMember.Local

namespace FlameCsv.Tests.Binding;

public static class CsvBooleanValuesAttributeTests
{
    public static TheoryData<string, bool, bool?> NonNullableTestData()
        => new()
        {
            { "1", true, true },
            { "Y", true, true },
            { "0", true, false },
            { "N", true, false },
            { "true", false, null },
            { "false", false, null },
            { "Kyllä", true, true },
            { "KYLLÄ", true, true },
        };

    public static TheoryData<string, bool, bool?, string> NullableTestData()
        => new()
        {
            { "1", true, true, "" },
            { "Y", true, true, "" },
            { "0", true, false, "" },
            { "N", true, false, "" },
            { "true", false, null, "" },
            { "false", false, null, "" },
            { "null", true, null, "null" },
            { "null", false, null, "" },
            { "Kyllä", true, true, "" },
            { "KYLLÄ", false, null, "" },
        };

    private class Shim
    {
        [CsvBooleanValues(TrueValues = ["1", "Y", "Kyllä"], FalseValues = ["0", "N"])]
        public bool IsEnabled { get; set; }

        [CsvBooleanValues(TrueValues = ["1", "Y", "Kyllä"], FalseValues = ["0", "N"], IgnoreCase = false)]
        public bool? IsEnabledN { get; set; }

        [CsvBooleanValues(TrueValues = ["1"])]
        public int InvalidType { get; set; }

        [CsvBooleanValues]
        public bool NoValues { get; set; }
    }

    [Theory, InlineData(nameof(Shim.InvalidType)), InlineData(nameof(Shim.NoValues))]
    public static void Should_Validate_Configuration(string property)
    {
        Assert.Throws<CsvConfigurationException>(
            () =>
            {
                var @override = typeof(Shim).GetProperty(property)!.GetCustomAttribute<CsvConverterAttribute>()!;
                _ = @override.TryCreateConverter(typeof(bool), CsvOptions<char>.Default, out _);
            });
    }

    [Theory, MemberData(nameof(NonNullableTestData))]
    public static void Should_Override_Bool(string input, bool success, bool? expected)
    {
        Impl<char>();
        Impl<byte>();

        void Impl<T>() where T : unmanaged, IBinaryInteger<T>
        {
            var options = CsvOptions<T>.Default;

            var @override = typeof(Shim).GetProperty("IsEnabled")!.GetCustomAttribute<CsvConverterAttribute>()!;

            Assert.True(@override.TryCreateConverter(typeof(bool), options, out var converter));
            var parser = (CsvConverter<T, bool>)converter;
            var inputSpan = options.GetFromString(input).Span;

            if (success)
            {
                Assert.True(parser.TryParse(inputSpan, out var value));
                Assert.Equal(expected, value);
            }
            else
            {
                Assert.False(parser.TryParse(inputSpan, out _));
            }
        }
    }

    [Theory, MemberData(nameof(NullableTestData))]
    public static void Should_Override_Nullable_Bool(
        string input,
        bool success,
        bool? expected,
        string nullToken)
    {
        Impl<char>();
        Impl<byte>();

        void Impl<T>() where T : unmanaged, IBinaryInteger<T>
        {
            var options = new CsvOptions<T> { Null = nullToken };

            var @override = typeof(Shim).GetProperty("IsEnabledN")!.GetCustomAttribute<CsvConverterAttribute>()!;

            Assert.True(@override.TryCreateConverter(typeof(bool?), options, out var converter));
            var parser = (CsvConverter<T, bool?>)converter;
            var inputSpan = options.GetFromString(input).Span;

            if (success)
            {
                Assert.True(parser.TryParse(inputSpan, out var value));
                Assert.Equal(expected, value);
            }
            else
            {
                Assert.False(parser.TryParse(inputSpan, out _));
            }
        }
    }

    [Fact]
    public static void Should_Use_Case_Sensitivity()
    {
        Impl<char>();
        Impl<byte>();

        static void Impl<T>() where T : unmanaged, IBinaryInteger<T>
        {
            var @override = typeof(Shim).GetProperty("IsEnabledN")!.GetCustomAttribute<CsvConverterAttribute>()!;

            Assert.True(@override.TryCreateConverter(typeof(bool?), CsvOptions<T>.Default, out var converter));
            var parser = (CsvConverter<T, bool?>)converter;

            Assert.False(parser.TryParse(CsvOptions<T>.Default.GetFromString("y").Span, out _));
            Assert.True(
                parser.TryParse(CsvOptions<T>.Default.GetFromString("Y").Span, out var value) &&
                value.GetValueOrDefault());
        }
    }

    [Fact]
    public static void Should_Use_Empty_Null_If_None_Defined()
    {
        var options = CsvOptions<char>.Default;

        var @override = typeof(Shim).GetProperty("IsEnabledN")!.GetCustomAttribute<CsvConverterAttribute>()!;

        Assert.True(@override.TryCreateConverter(typeof(bool?), options, out var converter));
        var parser = (NullableConverter<char, bool>)converter;
        Assert.True(parser.TryParse("", out bool? parsed));
        Assert.False(parsed.HasValue);
    }

    [Fact]
    public static void Should_Validate_AlternateComparer()
    {
        Assert.ThrowsAny<CsvConfigurationException>(
            () => new CustomBooleanTextConverter(
                new CsvOptions<char>
                {
                    BooleanValues = { ("t", true), ("f", false) }, Comparer = new NotAlternateComparer(),
                }));
    }

    [ExcludeFromCodeCoverage]
    private sealed class NotAlternateComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => String.Equals(x, y);
        public int GetHashCode(string obj) => obj.GetHashCode();
    }
}
