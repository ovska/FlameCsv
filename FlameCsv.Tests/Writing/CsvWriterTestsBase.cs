using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public abstract class CsvWriterTestsBase
{
    public static TheoryData<string, bool, char?, CsvFieldQuoting, bool, int, bool, bool?> Args()
    {
        var data = new TheoryData<string, bool, char?, CsvFieldQuoting, bool, int, bool, bool?>();

        foreach (var newline in (string[]) ["\r\n", "\n"])
        foreach (var header in GlobalData.Booleans)
        foreach (var escape in (char?[]) ['^', null])
        foreach (var quoting in GlobalData.Enum<CsvFieldQuoting>())
        foreach (var sourceGen in GlobalData.Booleans)
        foreach (var bufferSize in (int[]) [-1, 17, 128, 1024, 4096])
        foreach (var outputType in GlobalData.Booleans)
        foreach (var guarded in GlobalData.GuardedMemory)
        {
            data.Add(newline, header, escape, quoting, sourceGen, bufferSize, outputType, guarded);
        }

        return data;
    }
}
