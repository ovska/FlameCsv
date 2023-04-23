using System.Runtime.InteropServices;

namespace FlameCsv.Tests;

public static class CsvRecordTests
{
    [Fact]
    public static void Should_Preserve_Chars_As_Strings()
    {
        Assert.True(MemoryMarshal.TryGetString(new CsvRecord<char>("Abc", CsvTextReaderOptions.Default)[0], out _, out _, out _));
    }

    [Fact]
    public static void Should_Return_Field()
    {
        var record = new CsvRecord<char>("A,B,C", CsvTextReaderOptions.Default);

        Assert.Equal(3, record.GetFieldCount());
        Assert.Equal("A", record.GetField(0).ToString());
        Assert.Equal("B", record.GetField(1).ToString());
        Assert.Equal("C", record.GetField(2).ToString());

        Assert.Equal("A", record[0].ToString());
        Assert.Equal("B", record[1].ToString());
        Assert.Equal("C", record[2].ToString());
    }
}
