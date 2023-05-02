using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Parsers.Text;

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
        Assert.Throws<CsvUnhandledException>(() => Run(opts));
    }

    [Fact]
    public void Should_Throw_If_Returns_False()
    {
        var opts = new CsvTextReaderOptions
        {
            ExceptionHandler = _ => false,
            HasHeader = false,
        };
        Assert.Throws<CsvUnhandledException>(() => Run(opts));
    }

    [Fact]
    public void Should_Handle()
    {
        var opts = new CsvTextReaderOptions
        {
            ExceptionHandler = args => args.Exception is CsvParseException,
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
            ExceptionHandler = args => throw new AggregateException(args.Exception),
            HasHeader = false,
        };
        Assert.Throws<AggregateException>(() => Run(opts));
    }

    [Fact]
    public void Should_Report_Line_And_Position_With_Header()
    {
        var ex = Record.Exception(() =>
        {
            foreach (var _ in CsvReader.Read<Obj>("id,name\r\ntest,test", CsvTextReaderOptions.Default))
            {
            }
        });

        Assert.IsType<CsvUnhandledException>(ex);
        Assert.Equal(2, ((CsvUnhandledException)ex).Line);
        Assert.Equal("id,name\r\n".Length, ((CsvUnhandledException)ex).Position);

        Assert.IsType<CsvParseException>(ex.InnerException);
        Assert.IsType<IntegerTextParser>(((CsvParseException)ex.InnerException).Parser);
    }

    [Fact]
    public void Should_Report_Line_And_Position_Without_Header()
    {
        var ex = Record.Exception(() =>
        {
            foreach (var _ in CsvReader.Read<Obj>(
                "1,Bob\r\ntest,test",
                CsvTextReaderOptions.Default,
                new() { HasHeader = false }))
            {
            }
        });

        Assert.IsType<CsvUnhandledException>(ex);
        Assert.Equal(2, ((CsvUnhandledException)ex).Line);
        Assert.Equal("1,Bob\r\n".Length, ((CsvUnhandledException)ex).Position);

        Assert.IsType<CsvParseException>(ex.InnerException);
        Assert.IsType<IntegerTextParser>(((CsvParseException)ex.InnerException).Parser);
    }

    private static List<Obj> Run(CsvReaderOptions<char> opts) => new(CsvReader.Read<Obj>(Data, opts));
}
