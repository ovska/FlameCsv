namespace FlameCsv.Tests.Writing;

public abstract class CsvWriterTestsBase
{
    public static TheoryData<CsvNewline, bool, CsvFieldQuoting, bool, int, bool, bool, bool?> Args()
    {
        return
        [
            .. from newline in (CsvNewline[])[CsvNewline.LF, CsvNewline.CRLF]
            from header in GlobalData.Booleans
            from quoting in (CsvFieldQuoting[])[CsvFieldQuoting.Never, CsvFieldQuoting.Auto, CsvFieldQuoting.Always]
            from sourceGen in GlobalData.Booleans
            from bufferSize in (int[])[-1, 256]
            from outputType in GlobalData.Booleans
            from parallel in GlobalData.Booleans
            from guarded in GlobalData.GuardedMemory
            select (newline, header, quoting, sourceGen, bufferSize, outputType, parallel, guarded),
        ];
    }
}
