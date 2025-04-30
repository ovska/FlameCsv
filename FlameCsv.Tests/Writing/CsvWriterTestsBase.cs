using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public abstract class CsvWriterTestsBase
{
    public static TheoryData<CsvNewline, bool, char?, CsvFieldQuoting, bool, int, bool, bool?> Args()
    {
        return
        [
            .. from newline in (CsvNewline[])[CsvNewline.LF, CsvNewline.CRLF]
            from header in GlobalData.Booleans
            from escape in (char?[])['^', null]
            from quoting in GlobalData.Enum<CsvFieldQuoting>()
            from sourceGen in GlobalData.Booleans
            from bufferSize in (int[])[-1, 256]
            from outputType in GlobalData.Booleans
            from guarded in GlobalData.GuardedMemory
            select (newline, header, escape, quoting, sourceGen, bufferSize, outputType, guarded),
        ];
    }
}
