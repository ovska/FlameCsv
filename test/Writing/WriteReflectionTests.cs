using FlameCsv.Attributes;

namespace FlameCsv.Tests.Writing;

public class WriteReflectionTests
{
    private class Obj
    {
        [CsvIndex(0)]
        public int Id { get; set; }

        [CsvIndex(1)]
        public string? Name { get; set; }

        [CsvIndex(2)]
        public bool IsEnabled { get; set; }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Should_Write(bool header)
    {
        var data = new Obj[]
        {
            new()
            {
                Id = 1,
                Name = "Bob",
                IsEnabled = true,
            },
            new()
            {
                Id = 2,
                Name = "Alice",
                IsEnabled = false,
            },
        };

        var opts = new CsvOptions<char> { HasHeader = header, MemoryPool = null };
        var sb = CsvWriter.WriteToString(data, opts);

        var expected = (header ? "Id,Name,IsEnabled\r\n" : "") + "1,Bob,true\r\n2,Alice,false\r\n";
        Assert.Equal(expected, sb.ToString());
    }

    [Fact]
    public void Should_Write_Tuple()
    {
        var data = new[] { (1, "Bob", true), (2, "Alice", false) };
        var opts = new CsvOptions<char> { HasHeader = false, MemoryPool = null };
        var sb = CsvWriter.WriteToString(data, opts);
        Assert.Equal("1,Bob,true\r\n2,Alice,false\r\n", sb.ToString());
    }
}
