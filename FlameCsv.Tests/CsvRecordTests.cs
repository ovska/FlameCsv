using FlameCsv.Enumeration;

namespace FlameCsv.Tests;

public static class CsvRecordTests
{
    [Fact]
    public static void Should_Validate_FieldCount()
    {
        Assert.Throws<InvalidDataException>(
            () => new CsvRecordEnumerable<char>(
                "1,2,3\r\n1,2,3,4\r\n".AsMemory(),
                new CsvTextOptions { ValidateFieldCount = true })
            .AsEnumerable()
            .ToList());
    }

    [Fact]
    public static void Should_Return_Field()
    {
        var record = new CsvRecord<char>("A,B,C".AsMemory(), CsvTextOptions.Default);

        Assert.Equal(3, record.GetFieldCount());
        Assert.Equal("A", record.GetField(0).ToString());
        Assert.Equal("B", record.GetField(1).ToString());
        Assert.Equal("C", record.GetField(2).ToString());

        Assert.Equal("A", record[0].ToString());
        Assert.Equal("B", record[1].ToString());
        Assert.Equal("C", record[2].ToString());
    }

    [Fact]
    public static void Should_Parse_Fields()
    {
        var records = new CsvRecordEnumerable<char>(
            "A,B,C\r\n1,2,3\r\n".AsMemory(),
            CsvTextOptions.Default).AsEnumerable().ToList();

        Assert.Single(records);

        var record = records[0];

        Assert.True(record.HasHeader);

        Assert.Equal(new[] { "A", "B", "C" }, record.GetHeaderRecord());

        Assert.Equal(1, record.GetField<int>(0));
        Assert.Equal(1, record.GetField<int>("A"));
        Assert.Equal(2, record.GetField<int>(1));
        Assert.Equal(2, record.GetField<int>("B"));
        Assert.Equal(3, record.GetField<int>(2));
        Assert.Equal(3, record.GetField<int>("C"));
    }

    [Fact]
    public static void Should_Parse_Record()
    {
        var records = new CsvRecordEnumerable<char>(
            "A,B,C\r\n1,2,3\r\n".AsMemory(),
            CsvTextOptions.Default).AsEnumerable().ToList();

        Assert.Single(records);
        var record = records[0];

        var obj = record.ParseRecord<Obj>();

        Assert.Equal(1, obj.A);
        Assert.Equal(2, obj.B);
        Assert.Equal(3, obj.C);
    }

    private sealed class Obj
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }
}
