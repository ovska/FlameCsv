using FlameCsv.Enumeration;

namespace FlameCsv.Tests.Enumeration;

public static class RecordEnumerationTests
{
    [Theory, InlineData(true), InlineData(false)]
    public static async Task Should_Preserve(bool isAsync)
    {
        const string data = """
                            1,2,3
                            4,5,6
                            7,8,9
                            """;

        var enumerable = new CsvRecordEnumerable<char>(data.AsMemory(), new CsvOptions<char> { HasHeader = false });

        List<CsvRecord<char>> records = [];

        if (isAsync)
        {
            await foreach (var record in enumerable.PreserveAsync(TestContext.Current.CancellationToken))
            {
                records.Add(record);
            }
        }
        else
        {
            records.AddRange(enumerable.Preserve());
        }

        Assert.Equal(3, records.Count);

        Assert.Equal("1,2,3", records[0].RawRecord.ToString());
        Assert.Equal("4,5,6", records[1].RawRecord.ToString());
        Assert.Equal("7,8,9", records[2].RawRecord.ToString());
    }
}
