using FlameCsv.Attributes;
using FlameCsv.Converters.Formattable;
using FlameCsv.Exceptions;

namespace FlameCsv.Tests.Reading;

public class ExceptionHandlerTests
{
    private class Obj
    {
        [CsvIndex(0)]
        public int Id { get; set; }

        [CsvIndex(1)]
        public string? Name { get; set; }
    }

    private static List<Obj> Run(CsvExceptionHandler<char>? handler) =>
        [
            .. Csv.From("Id,Name\r\n0,A\r\n1,B\r\nX,C\r\n3,D")
                .Read<Obj>(new CsvOptions<char> { ExceptionHandler = handler }),
        ];

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
        var list = Run(args =>
        {
            Assert.Equal(["Id", "Name"], args.Header);
            Assert.Equal(2, args.Record.FieldCount);
            Assert.Equal(4, args.LineNumber);
            Assert.Equal(19, args.Position);
            Assert.Equal("X,C", args.RawRecord.ToString());
            Assert.Equal("X", args.Record[0]);
            Assert.Equal("C", args.Record[1]);

            if (args.Exception is CsvParseException pe)
            {
                Assert.Equal(4, pe.Line);
                Assert.Equal(19, pe.RecordPosition);
                Assert.Equal("X,C", pe.RecordValue);

                Assert.Equal(0, pe.FieldIndex);
                Assert.Equal("X", pe.FieldValue);
                Assert.Equal(19, pe.FieldPosition);
                Assert.Equal(typeof(int), pe.TargetType);
                Assert.Contains("Id", pe.Target);
                Assert.Equal("Id", pe.HeaderValue);
                Assert.IsType<NumberTextConverter<int>>(pe.Converter);
                return true;
            }

            return false;
        });

        Assert.Equal(3, list.Count);
        Assert.Equal([(0, "A"), (1, "B"), (3, "D")], list.Select(o => (o.Id, o.Name)));
    }

    [Fact]
    public void Should_Throw_Inner()
    {
        Assert.Throws<AggregateException>(() => Run(args => throw new AggregateException(args.Exception)));
    }

    [Fact]
    public void Should_Report_Line_And_Position_With_Header()
    {
        var ex = Record.Exception(() =>
        {
            foreach (var _ in Csv.From("id,name\r\ntest,test").Read<Obj>(CsvOptions<char>.Default)) { }
        });

        Assert.IsType<CsvParseException>(ex);
        Assert.Equal(2, ((CsvParseException)ex).Line);
        Assert.Equal("id,name\r\n".Length, ((CsvParseException)ex).RecordPosition);

        Assert.IsType<NumberTextConverter<int>>(((CsvParseException)ex).Converter);
    }

    [Fact]
    public void Should_Report_Line_And_Position_Without_Header()
    {
        var ex = Record.Exception(() =>
        {
            foreach (var _ in Csv.From("1,Bob\r\ntest,test").Read<Obj>(new CsvOptions<char> { HasHeader = false })) { }
        });

        Assert.IsType<CsvParseException>(ex);
        Assert.Equal(2, ((CsvParseException)ex).Line);
        Assert.Equal("1,Bob\r\n".Length, ((CsvParseException)ex).RecordPosition);

        Assert.IsType<CsvParseException>(ex);
        Assert.IsType<NumberTextConverter<int>>(((CsvParseException)ex).Converter);
    }
}
