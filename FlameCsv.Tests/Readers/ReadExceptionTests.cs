using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Readers;

namespace FlameCsv.Tests.Readers;

public class ReadExceptionTests
{
    private class Obj
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
    }

    private const string Data = "0,A\r\n1,B\r\nX,C";

    [Fact]
    public void Should_Throw_On_Unhandled()
    {
        var opts = CsvReaderOptions<char>.Default;
        opts.ExceptionHandler = null;
        Assert.Throws<CsvParseException>(() => Run(opts));
    }

    [Fact]
    public void Should_Throw_If_Returns_False()
    {
        var opts = CsvReaderOptions<char>.Default;
        opts.ExceptionHandler = (_, _) => false;
        Assert.Throws<CsvParseException>(() => Run(opts));
    }

    [Fact]
    public void Should_Throw_Inner()
    {
        var opts = CsvReaderOptions<char>.Default;
        opts.ExceptionHandler = (_, e) =>
        {
            throw new AggregateException(e);
        };
        Assert.Throws<AggregateException>(() => Run(opts));
    }

    private static void Run(CsvReaderOptions<char> opts)
    {
        foreach (var _ in CsvReader.Read<Obj>(Data, opts))
        {
        }
    }
}
