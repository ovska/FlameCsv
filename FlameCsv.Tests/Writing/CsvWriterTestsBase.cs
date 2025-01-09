using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public abstract class CsvWriterTestsBase
{
    public static TheoryData<string, bool, char?, CsvFieldQuoting, bool, int, bool?> Args()
    {
        var data = new TheoryData<string, bool, char?, CsvFieldQuoting, bool, int, bool?>();

        foreach (var newline in (string[])["\r\n", "\n"])
        foreach (var header in GlobalData.Booleans)
        foreach (var escape in (char?[])['^', null])
        foreach (var quoting in (CsvFieldQuoting[])[CsvFieldQuoting.Never, CsvFieldQuoting.AlwaysQuote, CsvFieldQuoting.Auto])
        foreach (var sourceGen in GlobalData.Booleans)
        foreach (var bufferSize in (int[])[-1, 17, 128, 1024, 4096])
        foreach (var guarded in GlobalData.GuardedMemory)
        {
            data.Add(newline, header, escape, quoting, sourceGen, bufferSize, guarded);
        }

        return data;
    }
}
