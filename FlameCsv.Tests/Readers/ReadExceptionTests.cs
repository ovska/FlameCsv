using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Readers;

public class ReadExceptionTests
{
    private class Obj
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
    }

    private static List<Obj> Run(CsvOptions<char> opts)
        => new(CsvReader.Read<Obj>("0,A\r\n1,B\r\nX,C\r\n3,D", opts));

    [Fact]
    public void Should_Throw_On_Unhandled()
    {
        var opts = new CsvTextOptions
        {
            ExceptionHandler = null,
            HasHeader = false,
        };
        Assert.Throws<CsvUnhandledException>(() => Run(opts));
    }

    [Fact]
    public void Should_Throw_If_Returns_False()
    {
        var opts = new CsvTextOptions
        {
            ExceptionHandler = (in CsvExceptionHandlerArgs<char> _) => false,
            HasHeader = false,
        };
        Assert.Throws<CsvUnhandledException>(() => Run(opts));
    }

    [Fact]
    public void Should_Handle()
    {
        var exceptions = new List<CsvParseException>();

        var opts = new CsvTextOptions
        {
            HasHeader = false,
            ExceptionHandler = (in CsvExceptionHandlerArgs<char> args) =>
            {
                if (args.Exception is CsvParseException pe)
                {
                    exceptions.Add(pe);
                    return true;
                }

                return false;
            },
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

        Assert.Single(exceptions);
        Assert.IsType<Int32TextConverter>(exceptions[0].Parser);
    }

    [Fact]
    public void Should_Throw_Inner()
    {
        var opts = new CsvTextOptions
        {
            ExceptionHandler = (in CsvExceptionHandlerArgs<char> args) => throw new AggregateException(args.Exception),
            HasHeader = false,
        };
        Assert.Throws<AggregateException>(() => Run(opts));
    }

    [Fact]
    public void Should_Report_Line_And_Position_With_Header()
    {
        var ex = Record.Exception(() =>
        {
            foreach (var _ in CsvReader.Read<Obj>("id,name\r\ntest,test", CsvTextOptions.Default))
            {
            }
        });

        Assert.IsType<CsvUnhandledException>(ex);
        Assert.Equal(2, ((CsvUnhandledException)ex).Line);
        Assert.Equal("id,name\r\n".Length, ((CsvUnhandledException)ex).Position);

        Assert.IsType<CsvParseException>(ex.InnerException);
        Assert.IsType<Int32TextConverter>(((CsvParseException)ex.InnerException).Parser);
    }

    [Fact]
    public void Should_Report_Line_And_Position_Without_Header()
    {
        var ex = Record.Exception(() =>
        {
            foreach (var _ in CsvReader.Read<Obj>(
                "1,Bob\r\ntest,test",
                new CsvTextOptions { HasHeader = false }))
            {
            }
        });

        Assert.IsType<CsvUnhandledException>(ex);
        Assert.Equal(2, ((CsvUnhandledException)ex).Line);
        Assert.Equal("1,Bob\r\n".Length, ((CsvUnhandledException)ex).Position);

        Assert.IsType<CsvParseException>(ex.InnerException);
        Assert.IsType<Int32TextConverter>(((CsvParseException)ex.InnerException).Parser);
    }
}
