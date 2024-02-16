using FlameCsv.Binding.Attributes;

namespace FlameCsv.Tests.Writing;

public class WriteReflectionTests
{
    private class Obj
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
        [CsvIndex(2)] public bool IsEnabled { get; set; }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Should_Write(bool header)
    {
        var data = new Obj[]
        {
            new() { Id = 1, Name = "Bob", IsEnabled = true },
            new() { Id = 2, Name = "Alice", IsEnabled = false },
        };

        var opts = new CsvTextOptions { HasHeader = header, ArrayPool = null };
        var sb = await CsvWriter.WriteToStringAsync(data, opts);

        var expected =
            (header ? "Id,Name,IsEnabled\r\n" : "") +
            "1,Bob,true\r\n2,Alice,false\r\n";
        Assert.Equal(expected, sb.ToString());
    }
}
