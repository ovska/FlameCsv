using FlameCsv.Reading;

namespace FlameCsv.Tests.Readers;

public static class CsvProcessorTests
{
    [Fact]
    public static void Default_CsvProcessor_Dispose_Should_Not_Throw()
    {
        Assert.Null(Record.Exception(() => default(CsvProcessor<char, object>).Dispose()));
        Assert.Null(Record.Exception(() => default(CsvHeaderProcessor<char, object>).Dispose()));
    }
}
