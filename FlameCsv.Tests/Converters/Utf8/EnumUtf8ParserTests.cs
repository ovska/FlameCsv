//using System.Runtime.Serialization;
//using System.Text;

//namespace FlameCsv.Tests.Parsers.Utf8;

//public static class EnumUtf8ParserTests
//{
//    private enum TestEnum
//    {
//        [EnumMember(Value = "Long and complex text")]
//        A = 1,

//        [EnumMember(Value = "Sherlock Holmes")]
//        B = 2,
//    }

//    private static ReadOnlySpan<byte> GetSpan<TEnum>(
//        TEnum value,
//        bool toLower = false,
//        string format = "G")
//        where TEnum : Enum
//    {
//        var str = value.ToString(format);
//        if (toLower) str = str.ToLower();
//        return Encoding.UTF8.GetBytes(str);
//    }

//    [Fact]
//    public static void Should_Parse_EnumNames()
//    {
//        var parser = new EnumUtf8Parser<TestEnum>();

//        var aBytes = Encoding.UTF8.GetBytes("Long and complex text");
//        Assert.True(parser.TryParse(aBytes, out var _a));
//        Assert.Equal(TestEnum.A, _a);

//        var bBytes = Encoding.UTF8.GetBytes("Sherlock Holmes");
//        Assert.True(parser.TryParse(bBytes, out var _b));
//        Assert.Equal(TestEnum.B, _b);
//    }

//    [Fact]
//    public static void Should_Parse_Integers()
//    {
//        var parser = new EnumUtf8Parser<DayOfWeek>(allowUndefinedValues: true, ignoreCase: false);

//        var monday = GetSpan(DayOfWeek.Monday, format: "D");
//        Assert.True(parser.TryParse(monday, out var _monday));
//        Assert.Equal(DayOfWeek.Monday, _monday);

//        var sunday = GetSpan(DayOfWeek.Sunday, format: "D");
//        Assert.True(parser.TryParse(sunday, out var _sunday));
//        Assert.Equal(DayOfWeek.Sunday, _sunday);

//        var invalid = GetSpan((DayOfWeek)int.MaxValue, format: "D");
//        Assert.True(parser.TryParse(invalid, out var _invalid));
//        Assert.Equal(int.MaxValue, (int)_invalid);
//    }

//    [Fact]
//    public static void Should_Parse_Case_Sensitive_Strings()
//    {
//        var parser = new EnumUtf8Parser<DayOfWeek>(allowUndefinedValues: true, ignoreCase: false);

//        var monday = GetSpan(DayOfWeek.Monday);
//        Assert.True(parser.TryParse(monday, out var _monday));
//        Assert.Equal(DayOfWeek.Monday, _monday);

//        var sunday = GetSpan(DayOfWeek.Sunday);
//        Assert.True(parser.TryParse(sunday, out var _sunday));
//        Assert.Equal(DayOfWeek.Sunday, _sunday);

//        var invalid = GetSpan((DayOfWeek)int.MaxValue);
//        Assert.True(parser.TryParse(invalid, out var _invalid));
//        Assert.Equal(int.MaxValue, (int)_invalid);
//    }

//    [Fact]
//    public static void Should_Validate_Enum_Defined()
//    {
//        var parser = new EnumUtf8Parser<DayOfWeek>(allowUndefinedValues: false);
//        Assert.False(parser.TryParse(GetSpan((DayOfWeek)int.MaxValue), out _));
//    }

//    [Fact]
//    public static void Should_Parse_Case_Insensitive_Strings()
//    {
//        var parser = new EnumUtf8Parser<DayOfWeek>(allowUndefinedValues: true, ignoreCase: true);

//        var monday = GetSpan(DayOfWeek.Monday, toLower: true);
//        Assert.True(parser.TryParse(monday, out var _monday));
//        Assert.Equal(DayOfWeek.Monday, _monday);

//        var sunday = GetSpan(DayOfWeek.Sunday, toLower: true);
//        Assert.True(parser.TryParse(sunday, out var _sunday));
//        Assert.Equal(DayOfWeek.Sunday, _sunday);

//        var invalid = GetSpan((DayOfWeek)int.MaxValue, toLower: true);
//        Assert.True(parser.TryParse(invalid, out var _invalid));
//        Assert.Equal(int.MaxValue, (int)_invalid);
//    }
//}
