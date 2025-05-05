using FlameCsv.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Readers;

public class ExceptionHandlerTests
{
    private class Obj
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
    }

    private static List<Obj> Run(CsvExceptionHandler<char>? handler)
        => [..CsvReader.Read<Obj>("Id,Name\r\n0,A\r\n1,B\r\nX,C\r\n3,D").WithExceptionHandler(handler)];

    [Fact]
    public void Should_Throw_On_Unhandled()
    {
        Assert.Throws<CsvParseException>(() => Run(null));
    }

    [Fact]
    public void Should_Throw_If_Returns_False()
    {
        Assert.Throws<CsvParseException>(() => Run(_ => false));
    }

    [Fact]
    public void Should_Handle()
    {
        var exceptions = new List<CsvParseException>();

        var list = Run(
            args =>
            {
                Assert.Equal(["Id", "Name"], args.Header);
                Assert.Equal(2, args.FieldCount);
                Assert.Equal(4, args.Line);
                Assert.Equal(19, args.Position);
                Assert.Equal("X,C", args.Record.ToString());

                if (args.Exception is CsvParseException pe)
                {
                    exceptions.Add(pe);
                    return true;
                }

                return false;
            });

        Assert.Equal(3, list.Count);
        Assert.Equal(
            [(0, "A"), (1, "B"), (3, "D")],
            list.Select(o => (o.Id, o.Name)));

        Assert.Single(exceptions);
        Assert.IsType<NumberTextConverter<int>>(exceptions[0].Converter);
    }

    [Fact]
    public void Should_Throw_Inner()
    {
        Assert.Throws<AggregateException>(
            () => Run(args => throw new AggregateException(args.Exception)));
    }

    [Fact]
    public void Should_Report_Line_And_Position_With_Header()
    {
        var ex = Record.Exception(
            () =>
            {
                foreach (var _ in CsvReader.Read<Obj>("id,name\r\ntest,test", CsvOptions<char>.Default))
                {
                }
            });

        Assert.IsType<CsvParseException>(ex);
        Assert.Equal(2, ((CsvParseException)ex).Line);
        Assert.Equal("id,name\r\n".Length, ((CsvParseException)ex).RecordPosition);

        Assert.IsType<NumberTextConverter<int>>(((CsvParseException)ex).Converter);
    }

    [Fact]
    public void Should_Report_Line_And_Position_Without_Header()
    {
        var ex = Record.Exception(
            () =>
            {
                foreach (var _ in CsvReader.Read<Obj>(
                             "1,Bob\r\ntest,test",
                             new CsvOptions<char> { HasHeader = false }))
                {
                }
            });

        Assert.IsType<CsvParseException>(ex);
        Assert.Equal(2, ((CsvParseException)ex).Line);
        Assert.Equal("1,Bob\r\n".Length, ((CsvParseException)ex).RecordPosition);

        Assert.IsType<CsvParseException>(ex);
        Assert.IsType<NumberTextConverter<int>>(((CsvParseException)ex).Converter);
    }
}
