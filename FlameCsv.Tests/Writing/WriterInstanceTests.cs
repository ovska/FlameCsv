using FlameCsv.Exceptions;

namespace FlameCsv.Tests.Writing;

public static class WriterInstanceTests
{
    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Validate_Field_Count(bool fromOptions)
    {
        using var writer = CsvWriter.Create(TextWriter.Null, new CsvOptions<char> { ValidateFieldCount = fromOptions });

        if (!fromOptions)
        {
            writer.ExpectedFieldCount = 3;
        }

        // 3 fields
        writer.WriteField("a");
        writer.WriteField("b");
        writer.WriteField("c");
        writer.NextRecord();

        // empty lines are allowed
        writer.NextRecord();

        writer.WriteField("a");
        writer.WriteField("b");
        Assert.ThrowsAny<CsvWriteException>(() => writer.NextRecord());
    }
}
