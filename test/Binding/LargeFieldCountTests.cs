using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Attributes;

namespace FlameCsv.Tests.Binding;

public class LargeFieldCountTests
{
    [Fact]
    public void Should_Support_24_Fields()
    {
        const string csv = "1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24";
        var options = new CsvOptions<char> { HasHeader = false };
        Obj obj = CsvReader.Read<Obj>(csv, options).Single();

        Assert.Equal(Enumerable.Range(1, 24), MemoryMarshal.CreateSpan(ref obj.Field0, 24).ToArray());

        StringBuilder sb = CsvWriter.WriteToString([obj], options);
        Assert.Equal(csv, sb.ToString().Trim());
    }

    // csharpier-ignore
    [StructLayout(LayoutKind.Sequential)]
    private struct Obj
    {
        [CsvIndex(0)] public int Field0;
        [CsvIndex(1)] public int Field1;
        [CsvIndex(2)] public int Field2;
        [CsvIndex(3)] public int Field3;
        [CsvIndex(4)] public int Field4;
        [CsvIndex(5)] public int Field5;
        [CsvIndex(6)] public int Field6;
        [CsvIndex(7)] public int Field7;
        [CsvIndex(8)] public int Field8;
        [CsvIndex(9)] public int Field9;
        [CsvIndex(10)] public int Field10;
        [CsvIndex(11)] public int Field11;
        [CsvIndex(12)] public int Field12;
        [CsvIndex(13)] public int Field13;
        [CsvIndex(14)] public int Field14;
        [CsvIndex(15)] public int Field15;
        [CsvIndex(16)] public int Field16;
        [CsvIndex(17)] public int Field17;
        [CsvIndex(18)] public int Field18;
        [CsvIndex(19)] public int Field19;
        [CsvIndex(20)] public int Field20;
        [CsvIndex(21)] public int Field21;
        [CsvIndex(22)] public int Field22;
        [CsvIndex(23)] public int Field23;
    }
}
