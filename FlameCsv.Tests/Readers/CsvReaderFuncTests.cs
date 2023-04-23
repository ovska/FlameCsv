#if false
namespace FlameCsv.Tests.Readers;

public static class CsvReaderFuncTests
{
    private const string Data =
        "1,true,Bob\r\n" +
        "2,false,Alice\r\n";

    [Fact]
    public static async Task Test()
    {
        var reader = new StringReader(Data);

        var enumerable = CsvReader.ReadRecordsAsync(
            reader,
            new CsvTextReaderOptions(),
            (int id, bool enabled, string name) => new { id, enabled, name });

        var results = new List<(int id, bool enabled, string name)>();

        await foreach (var item in enumerable)
        {
            results.Add((item.id, item.enabled, item.name));
        }

        Assert.Equal(2, results.Count);
        Assert.Equal((1, true, "Bob"), results[0]);
        Assert.Equal((2, false, "Alice"), results[1]);
    }
}
#endif
