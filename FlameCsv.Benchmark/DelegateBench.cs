using System.Runtime.CompilerServices;
using FlameCsv.Runtime;

namespace FlameCsv.Benchmark;

[SimpleJob]
public class DelegateBench
{
    private readonly Func<string, string, int, string, CsvReadBench.StatusEnum, CsvReadBench.UnitsEnum, int, string, string, string, string, string,
        string, string, CsvReadBench.Item> _compiled
        = ReflectionUtil
            .CreateInitializer<string, string, int, string, CsvReadBench.StatusEnum, CsvReadBench.UnitsEnum, int, string, string, string, string, string,
                string, string, CsvReadBench.Item>(
                typeof(CsvReadBench.Item).GetProperty("SeriesReference"),
                typeof(CsvReadBench.Item).GetProperty("Period"),
                typeof(CsvReadBench.Item).GetProperty("DataValue"),
                typeof(CsvReadBench.Item).GetProperty("Suppressed"),
                typeof(CsvReadBench.Item).GetProperty("Status"),
                typeof(CsvReadBench.Item).GetProperty("Units"),
                typeof(CsvReadBench.Item).GetProperty("Magnitude"),
                typeof(CsvReadBench.Item).GetProperty("Subject"),
                typeof(CsvReadBench.Item).GetProperty("Group"),
                typeof(CsvReadBench.Item).GetProperty("SeriesTitle1"),
                typeof(CsvReadBench.Item).GetProperty("SeriesTitle2"),
                typeof(CsvReadBench.Item).GetProperty("SeriesTitle3"),
                typeof(CsvReadBench.Item).GetProperty("SeriesTitle4"),
                typeof(CsvReadBench.Item).GetProperty("SeriesTitle5"));

    [Benchmark]
    public CsvReadBench.Item Compiled()
    {
        return _compiled(
            "SeriesReference",
            "Period",
            1,
            "Suppressed",
            CsvReadBench.StatusEnum.F,
            CsvReadBench.UnitsEnum.Number,
            1,
            "Subject",
            "Group",
            "SeriesTitle1",
            "SeriesTitle2",
            "SeriesTitle3",
            "SeriesTitle4",
            "SeriesTitle5");
    }

    [Benchmark]
    public CsvReadBench.Item Direct()
    {
        return CreateItem(
            "SeriesReference",
            "Period",
            1,
            "Suppressed",
            CsvReadBench.StatusEnum.F,
            CsvReadBench.UnitsEnum.Number,
            1,
            "Subject",
            "Group",
            "SeriesTitle1",
            "SeriesTitle2",
            "SeriesTitle3",
            "SeriesTitle4",
            "SeriesTitle5");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CsvReadBench.Item CreateItem(
        string SeriesReference,
        string Period,
        int DataValue,
        string Suppressed,
        CsvReadBench.StatusEnum Status,
        CsvReadBench.UnitsEnum Units,
        int Magnitude,
        string Subject,
        string Group,
        string SeriesTitle1,
        string SeriesTitle2,
        string SeriesTitle3,
        string SeriesTitle4,
        string SeriesTitle5)
    {
        return new()
        {
            SeriesReference = SeriesReference,
            Period = Period,
            DataValue = DataValue,
            Suppressed = Suppressed,
            Status = Status,
            Units = Units,
            Magnitude = Magnitude,
            Subject = Subject,
            Group = Group,
            SeriesTitle1 = SeriesTitle1,
            SeriesTitle2 = SeriesTitle2,
            SeriesTitle3 = SeriesTitle3,
            SeriesTitle4 = SeriesTitle4,
            SeriesTitle5 = SeriesTitle5,
        };
    }
}
