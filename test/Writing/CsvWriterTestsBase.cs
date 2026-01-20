using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Writing;

public abstract class CsvWriterTestsBase
{
    public static TheoryData<
        CsvNewline,
        bool,
        CsvFieldQuoting,
        bool,
        int,
        bool,
        Multithread,
        PoisonPagePlacement
    > Args()
    {
        return
        [
            .. from newline in (CsvNewline[])[CsvNewline.LF, CsvNewline.CRLF]
            from header in GlobalData.Booleans
            from quoting in QuotingModes
            from sourceGen in GlobalData.Booleans
            from bufferSize in (int[])[-1, 256]
            from outputType in GlobalData.Booleans
            from parallel in GlobalData.Enum<Multithread>()
            from placement in GlobalData.PoisonPlacement
            select (newline, header, quoting, sourceGen, bufferSize, outputType, parallel, placement),
        ];
    }

    private static CsvFieldQuoting[] QuotingModes { get; } =
    [CsvFieldQuoting.Never, CsvFieldQuoting.Auto | CsvFieldQuoting.Empty, CsvFieldQuoting.Auto, CsvFieldQuoting.Always];

    protected static CsvParallelOptions GetParallelOptions(Multithread multithread)
    {
        return new CsvParallelOptions
        {
            CancellationToken = TestContext.Current.CancellationToken,
            Unordered = multithread is Multithread.Unordered,
        };
    }
}
