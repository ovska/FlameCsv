using System.Runtime.InteropServices;

namespace FlameCsv.Tests;

public static class CsvRecordTests
{
    [Fact]
    public static void Should_Preserve_Chars_As_Strings()
    {
        Assert.True(MemoryMarshal.TryGetString(new CsvRecord<char>("Abc".AsMemory(), CsvTextReaderOptions.Default)[0], out _, out _, out _));
    }

    [Fact]
    public static void Should_Validate_FieldCount()
    {
        Assert.Throws<InvalidDataException>(
            () => new CsvRecordEnumerable<char>(
                "1,2,3\r\n1,2,3,4\r\n".AsMemory(),
                new CsvTextReaderOptions { ValidateFieldCount = true })
            .AsEnumerable()
            .ToList());

    }

    [Fact]
    public static void Should_Return_Field()
    {
        var record = new CsvRecord<char>("A,B,C".AsMemory(), CsvTextReaderOptions.Default);

        Assert.Equal(3, record.GetFieldCount());
        Assert.Equal("A", record.GetField(0).ToString());
        Assert.Equal("B", record.GetField(1).ToString());
        Assert.Equal("C", record.GetField(2).ToString());

        Assert.Equal("A", record[0].ToString());
        Assert.Equal("B", record[1].ToString());
        Assert.Equal("C", record[2].ToString());
    }
}
