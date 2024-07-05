using System.Text;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Converters;

// ReSharper disable UnusedMember.Local

namespace FlameCsv.Tests.Binding;

public static class CsvBooleanValuesAttributeTests
{
    public static TheoryData<string, bool, bool?> NonNullableTestData()
    {
        var data = new TheoryData<string, bool, bool?>();
        data.Add("1", true, true);
        data.Add("Y", true, true);
        data.Add("0", true, false);
        data.Add("N", true, false);
        data.Add("true", false, null);
        data.Add("false", false, null);
        return data;
    }

    public static TheoryData<string, bool, bool?, string> NullableTestData()
    {
        var data = new TheoryData<string, bool, bool?, string>();
        data.Add("1", true, true, "");
        data.Add("Y", true, true, "");
        data.Add("0", true, false, "");
        data.Add("N", true, false, "");
        data.Add("true", false, null, "");
        data.Add("false", false, null, "");
        data.Add("null", true, null, "null");
        data.Add("null", false, null, "");
        return data;
    }

    private class Shim
    {
        [CsvBooleanTextValues(TrueValues = ["1", "Y"], FalseValues = ["0", "N"])]
        [CsvBooleanUtf8Values(TrueValues = ["1", "Y"], FalseValues = ["0", "N"])]
        public bool IsEnabled { get; set; }

        [CsvBooleanTextValues(TrueValues = ["1", "Y"], FalseValues = ["0", "N"])]
        [CsvBooleanUtf8Values(TrueValues = ["1", "Y"], FalseValues = ["0", "N"])]
        public bool? IsEnabledN { get; set; }

        [CsvBooleanTextValues(TrueValues = ["1"])]
        [CsvBooleanUtf8Values(TrueValues = ["1"])]
        public int InvalidType { get; set; }

        [CsvBooleanTextValues]
        [CsvBooleanUtf8Values]
        public bool NoValues { get; set; }
    }

    [Theory, InlineData("InvalidType"), InlineData("NoValues")]
    public static void Should_Validate_Configuration(string property)
    {
        Assert.Throws<CsvConfigurationException>(
            () =>
            {
                var binding = CsvBinding.ForMember<Shim>(0, typeof(Shim).GetProperty(property)!);
                Assert.True(binding.TryGetAttribute<CsvConverterAttribute<char>>(out var @override));
                _ = @override!.CreateConverter(typeof(bool), CsvOptions<char>.Default);
            });
    }

    [Theory, MemberData(nameof(NonNullableTestData))]
    public static void Should_Override_Bool_Text_Parser(string input, bool success, bool? expected)
    {
        var options = CsvOptions<char>.Default;

        var binding = CsvBinding.ForMember<Shim>(0, typeof(Shim).GetProperty("IsEnabled")!);

        Assert.True(binding.TryGetAttribute<CsvConverterAttribute<char>>(out var @override));

        var parser = (CsvConverter<char, bool>)@override!.CreateConverter(typeof(bool), options);

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
        var options = new CsvOptions<char> { Null = nullToken };
        var binding = CsvBinding.ForMember<Shim>(0, typeof(Shim).GetProperty("IsEnabledN")!);

        Assert.True(binding.TryGetAttribute<CsvConverterAttribute<char>>(out var @override));

        var parser = (CsvConverter<char, bool?>)@override!.CreateConverter(typeof(bool?), options);

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
        var options = CsvOptions<byte>.Default;

        var binding = CsvBinding.ForMember<Shim>(0, typeof(Shim).GetProperty("IsEnabled")!);

        Assert.True(binding.TryGetAttribute<CsvConverterAttribute<byte>>(out var @override));

        var parser = (CsvConverter<byte, bool>)@override!.CreateConverter(typeof(bool), options);
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
        var options = new CsvOptions<byte> { Null = nullToken };
        var binding = CsvBinding.ForMember<Shim>(0, typeof(Shim).GetProperty("IsEnabledN")!);

        Assert.True(binding.TryGetAttribute<CsvConverterAttribute<byte>>(out var @override));

        var parser = (FlameCsv.CsvConverter<byte, bool?>)@override!.CreateConverter(typeof(bool?), options);
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
        var options = CsvOptions<char>.Default;

        var binding = CsvBinding.ForMember<Shim>(0, typeof(Shim).GetProperty("IsEnabledN")!);

        Assert.True(binding.TryGetAttribute<CsvConverterAttribute<char>>(out var @override));

        var parser = (NullableConverter<char, bool>)@override!.CreateConverter(typeof(bool?), options);
        Assert.True(parser.TryParse("", out bool? parsed));
        Assert.False(parsed.HasValue);
    }
}
