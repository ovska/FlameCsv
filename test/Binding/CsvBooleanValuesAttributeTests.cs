using System.Reflection;
using FlameCsv.Attributes;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

namespace FlameCsv.Tests.Binding;

public static class CsvBooleanValuesAttributeTests
{
    public static TheoryData<string, bool, bool?> NonNullableTestData =>
        new()
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

    public static TheoryData<string, bool, bool?, string> NullableTestData =>
        new()
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

    [Fact]
    public static void Should_Format()
    {
        Impl<char>();
        Impl<byte>();

        void Impl<T>()
            where T : unmanaged, IBinaryInteger<T>
        {
            var @override = typeof(Shim).GetProperty("IsEnabled")!.GetCustomAttribute<CsvConverterAttribute>()!;
            Assert.True(@override.TryCreateConverter(typeof(bool), CsvOptions<T>.Default, out var converter));
            var formatter = (CsvConverter<T, bool>)converter;

            T[] buffer = new T[32];
            Assert.True(formatter.TryFormat(buffer, true, out int charsWritten));
            Assert.Equal("1", Transcode.ToString(buffer.AsSpan(0, charsWritten)));

            Assert.True(formatter.TryFormat(buffer, false, out charsWritten));
            Assert.Equal("0", Transcode.ToString(buffer.AsSpan(0, charsWritten)));
        }
    }

    [Theory, InlineData(nameof(Shim.InvalidType)), InlineData(nameof(Shim.NoValues))]
    public static void Should_Validate_Configuration(string property)
    {
        var @override = typeof(Shim).GetProperty(property)!.GetCustomAttribute<CsvConverterAttribute>()!;

        Assert.Throws<CsvConfigurationException>(() =>
        {
            _ = @override.TryCreateConverter(typeof(bool), CsvOptions<char>.Default, out _);
        });
    }

    [Theory, MemberData(nameof(NonNullableTestData))]
    public static void Should_Override_Bool(string input, bool success, bool? expected)
    {
        Impl<char>();
        Impl<byte>();

        void Impl<T>()
            where T : unmanaged, IBinaryInteger<T>
        {
            var @override = typeof(Shim).GetProperty("IsEnabled")!.GetCustomAttribute<CsvConverterAttribute>()!;

            Assert.True(@override.TryCreateConverter(typeof(bool), CsvOptions<T>.Default, out var converter));
            var parser = (CsvConverter<T, bool>)converter;
            var inputSpan = Transcode.FromString<T>(input).Span;

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
    public static void Should_Override_Nullable_Bool(string input, bool success, bool? expected, string nullToken)
    {
        Impl<char>();
        Impl<byte>();

        void Impl<T>()
            where T : unmanaged, IBinaryInteger<T>
        {
            var options = new CsvOptions<T> { Null = nullToken };

            var @override = typeof(Shim).GetProperty("IsEnabledN")!.GetCustomAttribute<CsvConverterAttribute>()!;

            Assert.True(@override.TryCreateConverter(typeof(bool?), options, out var converter));
            var parser = (CsvConverter<T, bool?>)converter;
            var inputSpan = Transcode.FromString<T>(input).Span;

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

        static void Impl<T>()
            where T : unmanaged, IBinaryInteger<T>
        {
            var @override = typeof(Shim).GetProperty("IsEnabledN")!.GetCustomAttribute<CsvConverterAttribute>()!;

            Assert.True(@override.TryCreateConverter(typeof(bool?), CsvOptions<T>.Default, out var converter));
            var parser = (CsvConverter<T, bool?>)converter;

            Assert.False(parser.TryParse(Transcode.FromString<T>("y").Span, out _));
            Assert.True(parser.TryParse(Transcode.FromString<T>("Y").Span, out var value) && value.GetValueOrDefault());
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
    public static void Should_Validate_Options()
    {
        var opts = new CsvOptions<char> { BooleanValues = { ("t", true), ("f", false) } };

        Assert.ThrowsAny<CsvConfigurationException>(() =>
            new CustomBooleanConverter<char>(new CsvOptions<char>(opts) { Comparer = StringComparer.CurrentCulture })
        );

        // no exception
        _ = new CustomBooleanConverter<char>(new CsvOptions<char>(opts) { Comparer = StringComparer.Ordinal });
        _ = new CustomBooleanConverter<char>(
            new CsvOptions<char>(opts) { Comparer = StringComparer.OrdinalIgnoreCase }
        );
    }

    private sealed class NotAlternateComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => String.Equals(x, y);

        public int GetHashCode(string obj) => obj.GetHashCode();
    }
}
