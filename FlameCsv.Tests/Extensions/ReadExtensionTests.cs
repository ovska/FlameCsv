using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Extensions;

public static class ReadExtensionTests
{
    [Theory, MemberData(nameof(TrimmingData))]
    public static void Should_Trim(CsvFieldTrimming value, string input, string expected)
    {
        Assert.Equal(expected, input.AsSpan().Trim(value).ToString());

        ReadOnlySpan<char> span = input.AsSpan();

        switch (value)
        {
            case CsvFieldTrimming.None:
                NoTrimming.Trim(ref span);
                break;
            case CsvFieldTrimming.Leading:
                LeadingTrimming.Trim(ref span);
                break;
            case CsvFieldTrimming.Trailing:
                TrailingTrimming.Trim(ref span);
                break;
        }

        Assert.Equal(expected, span.ToString());
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
