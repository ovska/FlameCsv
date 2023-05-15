using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Runtime;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public class WriteReflectionTests
{
    private class Obj
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
        [CsvIndex(2)] public bool IsEnabled { get; set; }
    }

    //[Theory(Skip = "Broken"), InlineData(true), InlineData(false)]
    //public async Task Should_Write_ValueTuple(bool header)
    //{
    //    var data = new (int Id, string Name, bool IsEnabled)[]
    //    {
    //        (1, "Bob", true),
    //        (2, "Alice", false),
    //    };

    //    var opts = new CsvWriterOptions<char> { WriteHeader = header };
    //    using var writer = new StringWriter();

    //    await WriteTestGen<char, CsvCharBufferWriter>.WriteAsync(
    //        WriteOpHelpers.Create(writer, opts),
    //        opts,
    //        new AsyncIEnumerable<(int Id, string Name, bool IsEnabled)>(data),
    //        default);

    //    AssertValue(header, writer.ToString());
    //}

    [Theory, InlineData(true), InlineData(false)]
    public async Task Should_Write(bool header)
    {
        Assert.True(IndexAttributeBinder<Obj>.TryGetBindings(out var bc));

        var data = new Obj[]
        {
            new() { Id = 1, Name = "Bob", IsEnabled = true },
            new() { Id = 2, Name = "Alice", IsEnabled = false },
        };

        var opts = new CsvTextOptions { HasHeader = header };
        using var writer = new StringWriter();

        await WriteTest<char, CsvCharBufferWriter, Obj>.WriteRecords(
            WriteHelpers.Create(writer, opts),
            bc,
            opts,
            data,
            default);

        AssertValue(header, writer.ToString());
    }

    private static void AssertValue(bool header, string value)
    {
        if (header)
        {
            Assert.Equal("Id,Name,IsEnabled\r\n1,Bob,true\r\n2,Alice,false\r\n", value);
        }
        else
        {
            Assert.Equal("1,Bob,true\r\n2,Alice,false\r\n", value);
        }
    }
}
