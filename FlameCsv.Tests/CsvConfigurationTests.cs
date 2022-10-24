using System.Globalization;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Tests;

public class CsvConfigurationTests
{
    [Fact]
    public void Should_Prioritize_Parsers_Added_Last()
    {
        var config = new CsvConfigurationBuilder<char>()
            .AddParser(new IntegerTextParser(formatProvider: CultureInfo.CurrentCulture))
            .AddParser(new IntegerTextParser(formatProvider: CultureInfo.InvariantCulture))
            .Build();

        Assert.Equal(
            CultureInfo.InvariantCulture,
            ((IntegerTextParser)config.GetParser<int>()).FormatProvider);
    }

    [Fact]
    public void Should_Return_Text_Defaults()
    {
        var config = CsvConfiguration.GetTextDefaults();

        var boolParser = config.GetParser<bool>();
        Assert.True(boolParser.TryParse("true", out var bValue));
        Assert.True(bValue);

        var intParser = config.GetParser<ushort>();
        Assert.True(intParser.TryParse("1234", out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = config.GetParser<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday", out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);
    }
}
