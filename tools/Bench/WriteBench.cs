using System.Globalization;
using FlameCsv.Attributes;
using FlameCsv.Converters;
using FlameCsv.Converters.Formattable;
using FlameCsv.Writing;
using nietras.SeparatedValues;

// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public partial class WriteBench
{
    [Params( /*5, 1_000,*/
        10_000
    )]
    public int Count { get; set; }

    [Benchmark]
    public void CsvHelper_Records()
    {
        using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteHeader<Obj>();

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteRecord(obj);
            writer.NextRecord();
        }
    }

    [Benchmark]
    public void CsvHelper_RecordsMany()
    {
        using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);
        writer.WriteRecords(_data);
    }

    [Benchmark]
    public void CsvHelper_Fields()
    {
        using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteField("Index");
        writer.WriteField("Name");
        writer.WriteField("Contact");
        writer.WriteField("Count");
        writer.WriteField("Latitude");
        writer.WriteField("Longitude");
        writer.WriteField("Height");
        writer.WriteField("Location");
        writer.WriteField("Category");
        writer.WriteField("Popularity");
        writer.NextRecord();

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteField(obj.Index);
            writer.WriteField(obj.Name);
            writer.WriteField(obj.Contact);
            writer.WriteField(obj.Count);
            writer.WriteField(obj.Latitude);
            writer.WriteField(obj.Longitude);
            writer.WriteField(obj.Height);
            writer.WriteField(obj.Location);
            writer.WriteField(obj.Category);
            writer.WriteField(obj.Popularity);
            writer.NextRecord();
        }
    }

    [Benchmark]
    public async Task Async_CsvHelper_Records()
    {
        await using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteHeader<Obj>();

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteRecord(obj);
            await writer.NextRecordAsync().ConfigureAwait(false);
        }
    }

    [Benchmark]
    public async Task Async_CsvHelper_RecordsMany()
    {
        await using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);
        await writer.WriteRecordsAsync(_data).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Async_CsvHelper_Fields()
    {
        await using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);

        writer.WriteField("Index");
        writer.WriteField("Name");
        writer.WriteField("Contact");
        writer.WriteField("Count");
        writer.WriteField("Latitude");
        writer.WriteField("Longitude");
        writer.WriteField("Height");
        writer.WriteField("Location");
        writer.WriteField("Category");
        writer.WriteField("Popularity");
        await writer.NextRecordAsync().ConfigureAwait(false);

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];
            writer.WriteField(obj.Index);
            writer.WriteField(obj.Name);
            writer.WriteField(obj.Contact);
            writer.WriteField(obj.Count);
            writer.WriteField(obj.Latitude);
            writer.WriteField(obj.Longitude);
            writer.WriteField(obj.Height);
            writer.WriteField(obj.Location);
            writer.WriteField(obj.Category);
            writer.WriteField(obj.Popularity);
            await writer.NextRecordAsync().ConfigureAwait(false);
        }
    }

    [Benchmark]
    public void WriterObj()
    {
        using var writer = CsvWriter.Create(TextWriter.Null);

        writer.WriteHeader<Obj>();

        foreach (var obj in _data)
            writer.WriteRecord(obj);
    }

    [Benchmark]
    public void WriterObjTM()
    {
        using var writer = CsvWriter.Create(TextWriter.Null);

        writer.WriteHeader(ObjTypeMap.Default);

        foreach (var obj in _data)
            writer.WriteRecord(ObjTypeMap.Default, obj);
    }

    [Benchmark]
    public async Task Async_WriterObj()
    {
        await using var writer = CsvWriter.Create(TextWriter.Null);

        writer.WriteHeader<Obj>();
        await writer.NextRecordAsync();

        foreach (var obj in _data)
        {
            writer.WriteRecord(obj);
            await writer.NextRecordAsync();
        }
    }

    [Benchmark]
    public async Task Async_WriterObjTM()
    {
        await using var writer = CsvWriter.Create(TextWriter.Null);

        writer.WriteHeader(ObjTypeMap.Default);
        await writer.NextRecordAsync();

        foreach (var obj in _data)
        {
            writer.WriteRecord(ObjTypeMap.Default, obj);
            await writer.NextRecordAsync();
        }
    }

    [Benchmark(Baseline = true)]
    public void Generic()
    {
        CsvWriter.Write(TextWriter.Null, _data);
    }

    [Benchmark]
    public void Generic_TypeMap()
    {
        CsvWriter.Write(TextWriter.Null, _data, ObjTypeMap.Default);
    }

    [Benchmark]
    public Task Async_Generic()
    {
        return CsvWriter.WriteAsync(TextWriter.Null, _data);
    }

    [Benchmark]
    public Task Async_Generic_TypeMap()
    {
        return CsvWriter.WriteAsync(TextWriter.Null, _data, ObjTypeMap.Default);
    }

    [Benchmark]
    public void Generic_Fields()
    {
        using var writer = CsvFieldWriter.Create(TextWriter.Null, CsvOptions<char>.Default);

        var c1 = new NumberTextConverter<int>(CsvOptions<char>.Default, NumberStyles.Integer);
        var c2 = StringTextConverter.Instance;
        var c5 = new NumberTextConverter<double>(CsvOptions<char>.Default, NumberStyles.Float);
        var c6 = new NullableConverter<char, double>(c5, "".AsMemory());

        writer.WriteText("Index");
        writer.WriteDelimiter();
        writer.WriteText("Name");
        writer.WriteDelimiter();
        writer.WriteText("Contact");
        writer.WriteDelimiter();
        writer.WriteText("Count");
        writer.WriteDelimiter();
        writer.WriteText("Latitude");
        writer.WriteDelimiter();
        writer.WriteText("Longitude");
        writer.WriteDelimiter();
        writer.WriteText("Height");
        writer.WriteDelimiter();
        writer.WriteText("Location");
        writer.WriteDelimiter();
        writer.WriteText("Category");
        writer.WriteDelimiter();
        writer.WriteText("Popularity");
        writer.WriteNewline();

        for (int i = 0; i < _data.Length; i++)
        {
            if (writer.Writer.NeedsFlush)
                writer.Writer.Flush();

            Obj obj = _data[i];
            writer.WriteField(c1, obj.Index);
            writer.WriteDelimiter();
            writer.WriteField(c2, obj.Name);
            writer.WriteDelimiter();
            writer.WriteField(c2, obj.Contact);
            writer.WriteDelimiter();
            writer.WriteField(c1, obj.Count);
            writer.WriteDelimiter();
            writer.WriteField(c5, obj.Latitude);
            writer.WriteDelimiter();
            writer.WriteField(c5, obj.Longitude);
            writer.WriteDelimiter();
            writer.WriteField(c5, obj.Height);
            writer.WriteDelimiter();
            writer.WriteField(c2, obj.Location);
            writer.WriteDelimiter();
            writer.WriteField(c2, obj.Category);
            writer.WriteDelimiter();
            writer.WriteField(c6, obj.Popularity);
            writer.WriteNewline();
        }

        writer.Writer.Complete(null);
    }

    [Benchmark]
    public void Sepp()
    {
        using var writer = Sep.Writer(c => c with { Sep = new(','), Escape = true, WriteHeader = true })
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
            "Popularity"
        );

        int count = 0;

        foreach (var obj in _data)
        {
            using var row = writer.NewRow();
            row[0].Format(obj.Index);
            row[1].Set(obj.Name);
            row[2].Set(obj.Contact);
            row[3].Format(obj.Count);
            row[4].Format(obj.Latitude);
            row[5].Format(obj.Longitude);
            row[6].Format(obj.Height);
            row[7].Set(obj.Location);
            row[8].Set(obj.Category);
            row[9].Set($"{obj.Popularity}");

            if (++count == 100)
            {
                writer.Flush();
                count = 0;
            }
        }

        writer.Flush();
    }

    [GlobalSetup]
    public void Setup()
    {
        _data = CsvReader
            .Read<Obj>(
                File.ReadAllBytes(
                    "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv"
                ),
                new() { HasHeader = false }
            )
            .ToArray();
    }

    private Obj[] _data = null!;

    public sealed class Obj
    {
        [CsvHelper.Configuration.Attributes.Index(0), CsvIndex(0)]
        public int Index { get; set; }

        [CsvHelper.Configuration.Attributes.Index(1), CsvIndex(1)]
        public string? Name { get; set; }

        [CsvHelper.Configuration.Attributes.Index(2), CsvIndex(2)]
        public string? Contact { get; set; }

        [CsvHelper.Configuration.Attributes.Index(3), CsvIndex(3)]
        public int Count { get; set; }

        [CsvHelper.Configuration.Attributes.Index(4), CsvIndex(4)]
        public double Latitude { get; set; }

        [CsvHelper.Configuration.Attributes.Index(5), CsvIndex(5)]
        public double Longitude { get; set; }

        [CsvHelper.Configuration.Attributes.Index(6), CsvIndex(6)]
        public double Height { get; set; }

        [CsvHelper.Configuration.Attributes.Index(7), CsvIndex(7)]
        public string? Location { get; set; }

        [CsvHelper.Configuration.Attributes.Index(8), CsvIndex(8)]
        public string? Category { get; set; }

        [CsvHelper.Configuration.Attributes.Index(9), CsvIndex(9)]
        public double? Popularity { get; set; }
    }

    [CsvTypeMap<char, Obj>]
    public partial class ObjTypeMap;
}
