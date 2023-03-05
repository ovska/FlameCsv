using System.Text;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;

// ReSharper disable UnusedMember.Local

namespace FlameCsv.Tests.Binding;

public static class CsvBooleanValuesAttributeTests
{
    public static IEnumerable<object?[]> NonNullableTestData()
    {
        yield return new object?[] { "1", true, true };
        yield return new object?[] { "Y", true, true };
        yield return new object?[] { "0", true, false };
        yield return new object?[] { "N", true, false };
        yield return new object?[] { "true", false, null };
        yield return new object?[] { "false", false, null };
    }

    public static IEnumerable<object?[]> NullableTestData()
    {
        yield return new object?[] { "1", true, true, "" };
        yield return new object?[] { "Y", true, true, "" };
        yield return new object?[] { "0", true, false, "" };
        yield return new object?[] { "N", true, false, "" };
        yield return new object?[] { "true", false, null, "" };
        yield return new object?[] { "false", false, null, "" };
        yield return new object?[] { "null", true, null, "null" };
        yield return new object?[] { "null", false, null, "" };
    }

    private class Shim
    {
        [CsvBooleanValues(
            TrueValues = new[] { "1", "Y" },
            FalseValues = new[] { "0", "N" })]
        public bool IsEnabled { get; set; }

        [CsvBooleanValues(
            TrueValues = new[] { "1", "Y" },
            FalseValues = new[] { "0", "N" })]
        public bool? IsEnabledN { get; set; }

        [CsvBooleanValues(TrueValues = new[] { "1" })]
        public int InvalidType { get; set; }

        [CsvBooleanValues]
        public bool NoValues { get; set; }
    }

    [Theory, InlineData("InvalidType"), InlineData("NoValues")]
    public static void Should_Validate_Configuration(string property)
    {
        Assert.Throws<CsvConfigurationException>(
            () =>
            {
                var binding = new CsvBinding(0, typeof(Shim).GetProperty(property)!);
                var @override = binding.GetParserOverride<Shim>();
                _ = @override!.CreateParser(in binding, CsvReaderOptions<char>.Default);
            });
    }

    [Fact]
    public static void Should_Throw_On_Unsupported_Type()
    {
        Assert.Throws<NotSupportedException>(
            () =>
            {
                var binding = new CsvBinding(0, typeof(Shim).GetProperty("IsEnabled")!);
                var @override = binding.GetParserOverride<Shim>();
                _ = @override!.CreateParser(in binding, new CsvReaderOptions<int>());
            });
    }

    [Theory, MemberData(nameof(NonNullableTestData))]
    public static void Should_Override_Bool_Text_Parser(string input, bool success, bool? expected)
    {
        var options = CsvReaderOptions<char>.Default;

        var binding = new CsvBinding(0, typeof(Shim).GetProperty("IsEnabled")!);

        var @override = binding.GetParserOverride<Shim>();
        Assert.NotNull(@override);

        var parser = (ICsvParser<char, bool>)@override!.CreateParser(in binding, options);

        if (success)
        {
            Assert.True(parser.TryParse(input, out var value));
            Assert.Equal(expected, value);
        }
        else
        {
            Assert.False(parser.TryParse(input, out _));
        }
    }

    [Theory, MemberData(nameof(NullableTestData))]
    public static void Should_Override_Nullable_Text_Parser(
        string input,
        bool success,
        bool? expected,
        string nullToken)
    {
        var options = new CsvTextReaderOptions { Null = nullToken };
        var binding = new CsvBinding(0, typeof(Shim).GetProperty("IsEnabledN")!);

        var @override = binding.GetParserOverride<Shim>();
        Assert.NotNull(@override);

        var parser = (ICsvParser<char, bool?>)@override!.CreateParser(in binding, options);

        if (success)
        {
            Assert.True(parser.TryParse(input, out var value));
            Assert.Equal(expected, value);
        }
        else
        {
            Assert.False(parser.TryParse(input, out _));
        }
    }

    [Theory, MemberData(nameof(NonNullableTestData))]
    public static void Should_Override_Bool_Utf8_Parser(string input, bool success, bool? expected)
    {
        var options = CsvReaderOptions<byte>.Default;

        var binding = new CsvBinding(0, typeof(Shim).GetProperty("IsEnabled")!);

        var @override = binding.GetParserOverride<Shim>();
        Assert.NotNull(@override);

        var parser = (ICsvParser<byte, bool>)@override!.CreateParser(in binding, options);
        var inputBytes = Encoding.UTF8.GetBytes(input);

        if (success)
        {
            Assert.True(parser.TryParse(inputBytes, out var value));
            Assert.Equal(expected, value);
        }
        else
        {
            Assert.False(parser.TryParse(inputBytes, out _));
        }
    }

    [Theory, MemberData(nameof(NullableTestData))]
    public static void Should_Override_Nullable_Utf8_Parser(
        string input,
        bool success,
        bool? expected,
        string nullToken)
    {
        var options = new CsvUtf8ReaderOptions { Null = Encoding.UTF8.GetBytes(nullToken) };
        var binding = new CsvBinding(0, typeof(Shim).GetProperty("IsEnabledN")!);

        var @override = binding.GetParserOverride<Shim>();
        Assert.NotNull(@override);

        var parser = (ICsvParser<byte, bool?>)@override!.CreateParser<byte>(in binding, options);
        var inputBytes = Encoding.UTF8.GetBytes(input);

        if (success)
        {
            Assert.True(parser.TryParse(inputBytes, out var value));
            Assert.Equal(expected, value);
        }
        else
        {
            Assert.False(parser.TryParse(inputBytes, out _));
        }
    }

    [Fact]
    public static void Should_Use_Empty_Null_If_None_Defined()
    {
        var options = CsvReaderOptions<char>.Default;

        var binding = new CsvBinding(0, typeof(Shim).GetProperty("IsEnabledN")!);

        var @override = binding.GetParserOverride<Shim>();
        Assert.NotNull(@override);

        var parser = (NullableParser<char, bool>)@override!.CreateParser(in binding, options);
        Assert.Equal(default, parser.NullToken);
    }
}
