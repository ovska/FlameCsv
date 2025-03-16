using System.Globalization;
using nietras.SeparatedValues;
using RecordParser.Builders.Writer;
using RecordParser.Extensions;
using Sylvan.Data;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public class WriteObjects
{
    private static Entry[] _data = null!;

    [Benchmark(Baseline = true)]
    public void _Flame_SrcGen()
    {
        CsvWriter.Write(TextWriter.Null, _data, EntryTypeMap.Default);
    }

    [Benchmark]
    public void _Flame()
    {
        CsvWriter.Write(TextWriter.Null, _data);
    }

    [Benchmark]
    public void _Sep()
    {
        using var writer = Sep
            .Writer(
                c => c with
                {
                    Sep = new(','), Escape = true, WriteHeader = true,
                })
            .To(TextWriter.Null);

        writer.Header.Add(
            "Index",
            "Name",
            "Contact",
            "Count",
            "Latitude",
            "Longitude",
            "Height",
            "Location",
            "Category",
            "Popularity");

        int count = 0;

        foreach (var entry in _data)
        {
            using (var row = writer.NewRow())
            {
                row[0].Format(entry.Index);
                row[1].Set(entry.Name);
                row[2].Set(entry.Contact);
                row[3].Format(entry.Count);
                row[4].Format(entry.Latitude);
                row[5].Format(entry.Longitude);
                row[6].Format(entry.Height);
                row[7].Set(entry.Location);
                row[8].Set(entry.Category);
                row[9].Set($"{entry.Popularity}");
            }

            if (++count == 100)
            {
                writer.Flush();
                count = 0;
            }
        }

        writer.Flush();
    }

    [Benchmark]
    public void _Sylvan()
    {
        using var writer = Sylvan.Data.Csv.CsvDataWriter.Create(TextWriter.Null);
        writer.Write(_data.AsDataReader());
    }

    [Benchmark]
    public void _RecordParser()
    {
        var writer = new VariableLengthWriterBuilder<Entry>()
            .Map(e => e.Index, indexColumn: 0)
            .Map(e => e.Name, indexColumn: 1)
            .Map(e => e.Contact, indexColumn: 2)
            .Map(e => e.Count, indexColumn: 3)
            .Map(e => e.Latitude, indexColumn: 4)
            .Map(e => e.Longitude, indexColumn: 5)
            .Map(e => e.Height, indexColumn: 6)
            .Map(e => e.Location, indexColumn: 7)
            .Map(e => e.Category, indexColumn: 8)
            .Map(e => e.Popularity, indexColumn: 9)
            .Build(",", CultureInfo.InvariantCulture);

        TextWriter.Null.WriteRecords(_data, writer.TryFormat);
    }

    [Benchmark]
    public void _CsvHelper()
    {
        using CsvHelper.CsvWriter writer = new(TextWriter.Null, CultureInfo.InvariantCulture);
        writer.WriteRecords(_data);
    }

    [GlobalSetup]
    public void Setup()
    {
        _data = CsvReader.Read<Entry>(File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb.csv")).ToArray();
    }
}
