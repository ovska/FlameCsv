using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;

namespace FlameCsv.Tests.Readers;

public class ReadExceptionTests
{
    private class Obj
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
    }

    private const string Data = "0,A\r\n1,B\r\nX,C\r\n3,D";

    [Fact]
    public void Should_Throw_On_Unhandled()
    {
        var opts = new CsvTextReaderOptions
        {
            ExceptionHandler = null,
            HasHeader = false,
        };
        Assert.Throws<CsvParseException>(() => Run(opts));
    }

    [Fact]
    public void Should_Throw_If_Returns_False()
    {
        var opts = new CsvTextReaderOptions
        {
            ExceptionHandler = (_, _) => false,
            HasHeader = false,
        };
        Assert.Throws<CsvParseException>(() => Run(opts));
    }

    [Fact]
    public void Should_Handle()
    {
        var opts = new CsvTextReaderOptions
        {
            ExceptionHandler = (_, e) => e is CsvParseException,
            HasHeader = false,
        };

        var list = Run(opts);
        Assert.Equal(3, list.Count);
        Assert.Equal(
            new (int, string?)[]
            {
                (0, "A"),
                (1, "B"),
                (3, "D"),
            },
            list.Select(o => (o.Id, o.Name)));
    }

    [Fact]
    public void Should_Throw_Inner()
    {
        var opts = new CsvTextReaderOptions
        {
            ExceptionHandler = (_, e) => throw new AggregateException(e),
            HasHeader = false,
        };
        Assert.Throws<AggregateException>(() => Run(opts));
    }

    private static List<Obj> Run(CsvReaderOptions<char> opts) => new(CsvReader.Read<Obj>(Data, opts));
}
