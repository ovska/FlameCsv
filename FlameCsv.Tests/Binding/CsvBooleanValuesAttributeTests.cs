using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;

// ReSharper disable UnusedMember.Local

namespace FlameCsv.Tests.Binding;

public static class CsvBooleanValuesAttributeTests
{
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

        // TODO: test ^
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

    [Theory]
    [InlineData("1", true, true)]
    [InlineData("Y", true, true)]
    [InlineData("0", true, false)]
    [InlineData("N", true, false)]
    [InlineData("true", false, null)]
    [InlineData("false", false, null)]
    public static void Should_Override_Bool_Parser(string input, bool success, bool? expected)
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

    [Theory]
    [InlineData("1", true, true, "")]
    [InlineData("Y", true, true, "")]
    [InlineData("0", true, false, "")]
    [InlineData("N", true, false, "")]
    [InlineData("true", false, null, "")]
    [InlineData("false", false, null, "")]
    [InlineData("null", true, null, "null")]
    [InlineData("null", false, null, "")]
    public static void Should_Override_Nullable_Parser(string input, bool success, bool? expected, string nullToken)
    {
        var options = CsvOptions.GetTextReaderDefault(new CsvTextParsersConfig { Null = nullToken });
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
}
