using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Tests.Extensions;

public static class ReadExtensionTests
{
    [Theory, MemberData(nameof(TrimmingData))]
    public static void Should_Trim(CsvFieldTrimming value, string input, string expected)
    {
        Assert.Equal(expected, input.AsSpan().Trim(value).ToString());
    }

    public static TheoryData<CsvFieldTrimming, string, string> TrimmingData
        => new()
        {
            { CsvFieldTrimming.None, "  abc  ", "  abc  " },
            { CsvFieldTrimming.Leading, "  abc  ", "abc  " },
            { CsvFieldTrimming.Trailing, "  abc  ", "  abc" },
            { CsvFieldTrimming.Both, "  abc  ", "abc" },
            { CsvFieldTrimming.None, "", "" },
            { CsvFieldTrimming.Leading, "", "" },
            { CsvFieldTrimming.Trailing, "", "" },
            { CsvFieldTrimming.Both, "", "" },
            { CsvFieldTrimming.None, " ", " " },
            { CsvFieldTrimming.Leading, " ", "" },
            { CsvFieldTrimming.Trailing, " ", "" },
            { CsvFieldTrimming.Both, " ", "" },
        };
}
