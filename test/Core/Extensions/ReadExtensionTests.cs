using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Extensions;

public static class ReadExtensionTests
{
    [Theory, MemberData(nameof(TrimmingData))]
    public static void Should_Trim(CsvFieldTrimming value, string input, string expected)
    {
        Assert.Equal(expected, input.AsSpan().Trim(value).ToString());
        Assert.Equal(expected, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input).AsSpan().Trim(value)));
    }

    [Theory, MemberData(nameof(TrimmingData))]
    public static void Should_Check_If_Needs_Trimming(CsvFieldTrimming value, string input, string expected)
    {
        if (value == CsvFieldTrimming.None)
        {
            Assert.False(input.AsSpan().NeedsTrimming(value));
            return;
        }

        Assert.Equal(input.Length != expected.Length, input.AsSpan().NeedsTrimming(value));
    }

    public static TheoryData<CsvFieldTrimming, string, string> TrimmingData =>
        new()
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
