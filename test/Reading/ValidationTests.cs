using FlameCsv.Exceptions;
using FlameCsv.Reading;

namespace FlameCsv.Tests.Reading;

public static class ValidationTests
{
    private const string data = """
        id,name
        1,foo"bar"
        2,"baz""qux"
        3,"hello"world!

        """;

    private static Csv.IReadBuilder<char> Builder => Csv.From(data);

    private static TestRecord[] Expected =>
        [new TestRecord(1, "foo\"bar\""), new TestRecord(2, "baz\"qux"), new TestRecord(3, "\"hello\"world!")];

    [Fact]
    public static void Should_Allow_Invalid_Quotes()
    {
        Assert.Throws<CsvFormatException>(() => Builder.Read<TestRecord>().ToList());

        var results = Builder.Read<TestRecord>(new() { ValidateQuotes = CsvQuoteValidation.AllowInvalid }).ToList();
        Assert.Equal(Expected, results);
    }

    [Fact]
    public static void Should_Validate_Skipped_Records()
    {
        CsvRecordCallback<char> callback = (ref readonly args) =>
            args.SkipRecord = args.HeaderRead && args.Record.Raw[0] != '2';

        Assert.Throws<CsvFormatException>(() =>
        {
            Builder
                .Read<TestRecord>(
                    new() { ValidateQuotes = CsvQuoteValidation.ValidateAllRecords, RecordCallback = callback }
                )
                .ToList();
        });

        Assert.Throws<CsvFormatException>(() =>
        {
            foreach (
                var _ in Builder.Enumerate(
                    new() { ValidateQuotes = CsvQuoteValidation.ValidateAllRecords, RecordCallback = callback }
                )
            ) { }
        });

        foreach (var v in Builder.Read<TestRecord>(new() { RecordCallback = callback }))
        {
            Assert.Equal(Expected[1], v);
        }

        foreach (var v in Builder.Enumerate(new() { RecordCallback = callback }))
        {
            Assert.Equal(Expected[1], v.ParseRecord<TestRecord>());
        }
    }

    [Fact]
    public static void Should_Validate_Quotes()
    {
        using var enumerator = Builder
            .Enumerate(new CsvOptions<char> { ValidateQuotes = CsvQuoteValidation.ValidateAllRecords })
            .GetEnumerator();

        Assert.True(enumerator.MoveNext());
        A(() => ((CsvRecordRef<char>)enumerator.Current).ValidateAllFields());
        A(() => ((CsvRecordRef<char>)enumerator.Current).ValidateFieldsUnsafe(0, 1));

        // no ex
        ((CsvRecordRef<char>)enumerator.Current).ValidateFieldsUnsafe(0);

        Assert.True(enumerator.MoveNext());
        ((CsvRecordRef<char>)enumerator.Current).ValidateAllFields();
        ((CsvRecordRef<char>)enumerator.Current).ValidateFieldsUnsafe(0, 1);

        Assert.True(enumerator.MoveNext());
        A(() => ((CsvRecordRef<char>)enumerator.Current).ValidateAllFields());
        A(() => ((CsvRecordRef<char>)enumerator.Current).ValidateFieldsUnsafe(0, 1));

        Assert.False(enumerator.MoveNext());

        void A(Action action)
        {
            Assert.Throws<CsvFormatException>(action);
        }
    }

    private sealed record TestRecord(int Id, string Name);
}
