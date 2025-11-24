using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Attributes;
using FlameCsv.Writing;
using nietras.SeparatedValues;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public partial class WriteBench
{
    // [Benchmark]
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

    // [Benchmark]
    public void CsvHelper_RecordsMany()
    {
        using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);
        writer.WriteRecords(_data);
    }

    // [Benchmark]
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

    // [Benchmark]
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

    // [Benchmark]
    public async Task Async_CsvHelper_RecordsMany()
    {
        await using var writer = new CsvHelper.CsvWriter(TextWriter.Null, CultureInfo.InvariantCulture);
        await writer.WriteRecordsAsync(_data).ConfigureAwait(false);
    }

    // [Benchmark]
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

    // [Benchmark]
    public void WriterObj()
    {
        using var writer = CsvWriter.Create(TextWriter.Null);

        writer.WriteHeader<Obj>();

        foreach (var obj in _data)
            writer.WriteRecord(obj);
    }

    // [Benchmark]
    public void WriterObjTM()
    {
        using var writer = CsvWriter.Create(TextWriter.Null);

        writer.WriteHeader(ObjTypeMap.Default);

        foreach (var obj in _data)
            writer.WriteRecord(ObjTypeMap.Default, obj);
    }

    // [Benchmark]
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

    // [Benchmark]
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

    // [Benchmark(Baseline = true)]
    public void Generic()
    {
        CsvWriter.Write(TextWriter.Null, _data);
    }

    // [Benchmark]
    public void Generic_TypeMap()
    {
        CsvWriter.Write(TextWriter.Null, _data, ObjTypeMap.Default);
    }

    // [Benchmark]
    public Task Async_Generic()
    {
        return CsvWriter.WriteAsync(TextWriter.Null, _data);
    }

    [Benchmark]
    public void Parallel()
    {
        CsvParallel.WriteUnordered(
            _data,
            CsvOptions<char>.Default,
            CsvOptions<char>.Default.GetDematerializer(ObjTypeMap.Default.GetDematerializer),
            TextWriter.Null.Write
        );
    }

    // [Benchmark]
    public Task ParallelAsync()
    {
        return CsvParallel.WriteUnorderedAsync(
            _data,
            CsvOptions<char>.Default,
            CsvOptions<char>.Default.GetDematerializer(ObjTypeMap.Default.GetDematerializer),
            (m, ct) => new ValueTask(TextWriter.Null.WriteAsync(m, ct))
        );
    }

    // [Benchmark]
    public Task Async_Generic_TypeMap()
    {
        return CsvWriter.WriteAsync(TextWriter.Null, _data, ObjTypeMap.Default);
    }

    // [Benchmark]
    public void Yardstick()
    {
        using var writer = new ArrayPoolBufferWriter<char>(initialCapacity: 32 * 1024);

        "Index".CopyTo(writer.GetSpan("Index".Length));
        writer.Advance("Index".Length);
        writer.GetSpan(1)[0] = ',';
        writer.Advance(1);
        "Name".CopyTo(writer.GetSpan("Name".Length));
        writer.Advance("Name".Length);
        writer.GetSpan(1)[0] = ',';
        writer.Advance(1);
        "Contact".CopyTo(writer.GetSpan("Contact".Length));
        writer.Advance("Contact".Length);
        writer.GetSpan(1)[0] = ',';
        writer.Advance(1);
        "Count".CopyTo(writer.GetSpan("Count".Length));
        writer.Advance("Count".Length);
        writer.GetSpan(1)[0] = ',';
        writer.Advance(1);
        "Latitude".CopyTo(writer.GetSpan("Latitude".Length));
        writer.Advance("Latitude".Length);
        writer.GetSpan(1)[0] = ',';
        writer.Advance(1);
        "Longitude".CopyTo(writer.GetSpan("Longitude".Length));
        writer.Advance("Longitude".Length);
        writer.GetSpan(1)[0] = ',';
        writer.Advance(1);
        "Height".CopyTo(writer.GetSpan("Height".Length));
        writer.Advance("Height".Length);
        writer.GetSpan(1)[0] = ',';
        writer.Advance(1);
        "Location".CopyTo(writer.GetSpan("Location".Length));
        writer.Advance("Location".Length);
        "\r\n".CopyTo(writer.GetSpan(2));
        writer.Advance(2);

        for (int i = 0; i < _data.Length; i++)
        {
            Obj obj = _data[i];

            obj.Index.TryFormat(writer.GetSpan(256), out int indexBytes);
            writer.Advance(indexBytes);
            obj.Name.AsSpan().CopyTo(writer.GetSpan(obj.Name.AsSpan().Length));
            writer.Advance(obj.Name.AsSpan().Length);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            obj.Contact.AsSpan().CopyTo(writer.GetSpan(obj.Contact.AsSpan().Length));
            writer.Advance(obj.Contact.AsSpan().Length);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            obj.Count.TryFormat(writer.GetSpan(256), out int countBytes);
            writer.Advance(countBytes);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            obj.Latitude.TryFormat(writer.GetSpan(256), out int latBytes);
            writer.Advance(latBytes);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            obj.Longitude.TryFormat(writer.GetSpan(256), out int lonBytes);
            writer.Advance(lonBytes);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            obj.Height.TryFormat(writer.GetSpan(256), out int heightBytes);
            writer.Advance(heightBytes);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            obj.Location.AsSpan().CopyTo(writer.GetSpan(obj.Location.AsSpan().Length));
            writer.Advance(obj.Location.AsSpan().Length);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            obj.Category.AsSpan().CopyTo(writer.GetSpan(obj.Category.AsSpan().Length));
            writer.Advance(obj.Category.AsSpan().Length);
            writer.GetSpan(1)[0] = ',';
            writer.Advance(1);
            if (obj.Popularity.HasValue)
            {
                obj.Popularity.Value.TryFormat(writer.GetSpan(256), out int popBytes);
                writer.Advance(popBytes);
            }
            "\r\n".CopyTo(writer.GetSpan(2));
        }
    }

    // [Benchmark]
    public void Generic_Fields()
    {
        using var writer = CsvFieldWriter.Create(TextWriter.Null, CsvOptions<char>.Default);

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
            writer.FormatValue(obj.Index);
            writer.WriteDelimiter();
            writer.WriteText(obj.Name);
            writer.WriteDelimiter();
            writer.WriteText(obj.Contact);
            writer.WriteDelimiter();
            writer.FormatValue(obj.Count);
            writer.WriteDelimiter();
            writer.FormatValue(obj.Latitude);
            writer.WriteDelimiter();
            writer.FormatValue(obj.Longitude);
            writer.WriteDelimiter();
            writer.FormatValue(obj.Height);
            writer.WriteDelimiter();
            writer.WriteText(obj.Location);
            writer.WriteDelimiter();
            writer.WriteText(obj.Category);
            writer.WriteDelimiter();
            writer.FormatValue(obj.Popularity);
            writer.WriteNewline();
        }

        writer.Writer.Complete(null);
    }

    // [Benchmark]
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
        const string Path = @"/Users/sipi/Code/FlameCsv/tools/Bench/Comparisons/Data/SampleCSVFile_556kb_4x.csv";

        // _data = CsvReader.ReadFromFile<Obj>(Path, CsvOptions<byte>.Default).ToArray();
        using var r = new CsvHelper.CsvReader(
            new StreamReader(Path),
            new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true }
        );

        _data = r.GetRecords<Obj>().ToArray();
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

    private sealed class NonListLike<T>(T[] value) : IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            return new NonListLikeEnumerator<T>(value);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class NonListLikeEnumerator<T>(T[] value) : IEnumerator<T>
    {
        private int _index = -1;

        public T Current => value[_index];

        object? System.Collections.IEnumerator.Current => Current;

        public void Dispose() { }

        public bool MoveNext()
        {
            _index++;
            return _index < value.Length;
        }

        public void Reset()
        {
            _index = -1;
        }
    }
}
