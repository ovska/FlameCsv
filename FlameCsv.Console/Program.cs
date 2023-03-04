using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.TypeConversion;
using FlameCsv;
using FlameCsv.Binding;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;

var SIZEOFMEM = Unsafe.SizeOf<ReadOnlyMemory<char>>();
var SIZEOFMEM2 = Unsafe.SizeOf<ReadOnlyMemory<byte>>();
var SIZEOFRANGE = Unsafe.SizeOf<Range>();
var SIZEOFTOKENSB = Unsafe.SizeOf<CsvTokens<byte>>();
var SIZEOFTOKENSC = Unsafe.SizeOf<CsvTokens<char>>();

var xyz = Enumerable.Range(0, byte.MaxValue)
    .Select(i => (char)i)
    .Where(i => char.IsWhiteSpace((char)i))
    .ToArray();

var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = Environment.NewLine,
    HasHeaderRecord = false,
    CacheFields = true,
    CountBytes = false,
};

List<Item> records;
using (var reader = new StreamReader("/home/sipi/test.csv"))
using (var csv = new CsvReader(reader, config))
{
    var options = new TypeConverterOptions { Formats = new[] { "yyyy'.'MM" } };
    csv.Context.TypeConverterOptionsCache.AddOptions<DateTime>(options);

    records = csv.GetRecords<Item>().ToList();
    Debugger.Break();
}

///////////////////////////////////////////////////////////////////////7

await using var stream = File.OpenRead("/home/sipi/test.csv");

var config_ = new CsvUtf8ReaderOptions
{
    Tokens = CsvTokens<byte>.Unix,
    Parsers = { new YYYYMMParser() },
};
var result = new List<Item>();
await foreach (var item in FlameCsv.Readers.CsvReader.ReadAsync<Item>(stream, config_))
{
    result.Add(item);
}

Debugger.Break();
try
{
    var _config_ = new CsvTextReaderOptions()
    {
        Tokens = CsvTokens<char>.Unix,
        DateTimeFormat = "yyyy'.'mm",
    };

    using (var reader = new StreamReader("/home/sipi/test.csv"))
    {
        var result__ = new List<Item>();
        await foreach (var item in FlameCsv.Readers.CsvReader.ReadAsync<Item>(reader, _config_))
        {
            result__.Add(item);
        }

        Debugger.Break();

        var cmp = new Comparer();
        var notEq = result__.Zip(result).Where((a) => !cmp.Equals(a.First, a.Second)).ToList();
        _ = 1;
    }
}
catch (Exception e)
{
    Console.WriteLine(e);
    throw;
}

public enum StatusEnum
{
    X = 0,
    F = 1,
}

public enum UnitsEnum
{
    Unknown = 0,
    Number = 1,
}

// Series_reference,Period,Data_value,Suppressed,STATUS,UNITS,Magnitude,Subject,Group,Series_title_1,Series_title_2,Series_title_3,Series_title_4,Series_title_5
public class Item
{
    [CsvHelper.Configuration.Attributes.Index(0)]
    [FlameCsv.Binding.Attributes.CsvIndex(0)]
    public string? SeriesReference { get; set; }

    [CsvHelper.Configuration.Attributes.Index(1)]
    [FlameCsv.Binding.Attributes.CsvIndex(1)]
    public DateTime Period { get; set; } // date

    [CsvHelper.Configuration.Attributes.Index(2)]
    [FlameCsv.Binding.Attributes.CsvIndex(2)]
    public int DataValue { get; set; }

    [CsvHelper.Configuration.Attributes.Index(3)]
    [FlameCsv.Binding.Attributes.CsvIndex(3)]
    public string? Suppressed { get; set; }

    [CsvHelper.Configuration.Attributes.Index(4)]
    [FlameCsv.Binding.Attributes.CsvIndex(4)]
    public StatusEnum Status { get; set; }

    [CsvHelper.Configuration.Attributes.Index(5)]
    [FlameCsv.Binding.Attributes.CsvIndex(5)]
    public UnitsEnum Units { get; set; }

    [CsvHelper.Configuration.Attributes.Index(6)]
    [FlameCsv.Binding.Attributes.CsvIndex(6)]
    public int Magnitude { get; set; }

    [CsvHelper.Configuration.Attributes.Index(7)]
    [FlameCsv.Binding.Attributes.CsvIndex(7)]
    public string? Subject { get; set; }

    [CsvHelper.Configuration.Attributes.Index(8)]
    [FlameCsv.Binding.Attributes.CsvIndex(8)]
    public string? Group { get; set; }

    [CsvHelper.Configuration.Attributes.Index(9)]
    [FlameCsv.Binding.Attributes.CsvIndex(9)]
    public string? SeriesTitle1 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(10)]
    [FlameCsv.Binding.Attributes.CsvIndex(10)]
    public string? SeriesTitle2 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(11)]
    [FlameCsv.Binding.Attributes.CsvIndex(11)]
    public string? SeriesTitle3 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(12)]
    [FlameCsv.Binding.Attributes.CsvIndex(12)]
    public string? SeriesTitle4 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(13)]
    [FlameCsv.Binding.Attributes.CsvIndex(13)]
    public string? SeriesTitle5 { get; set; }
}

#nullable disable
#pragma warning disable CA1050
public sealed class Comparer : IEqualityComparer<Item>
#pragma warning restore CA1050
{
    public bool Equals(Item x, Item y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (ReferenceEquals(x, null))
            return false;
        if (ReferenceEquals(y, null))
            return false;
        if (x.GetType() != y.GetType())
            return false;
        return x.SeriesReference == y.SeriesReference
            && x.Period == y.Period
            && x.DataValue == y.DataValue
            && x.Suppressed == y.Suppressed
            && x.Status == y.Status
            && x.Units == y.Units
            && x.Magnitude == y.Magnitude
            && x.Subject == y.Subject
            && x.Group == y.Group
            && x.SeriesTitle1 == y.SeriesTitle1
            && x.SeriesTitle2 == y.SeriesTitle2
            && x.SeriesTitle3 == y.SeriesTitle3
            && x.SeriesTitle4 == y.SeriesTitle4
            && x.SeriesTitle5 == y.SeriesTitle5;
    }

    public int GetHashCode(Item obj)
    {
        var hashCode = new HashCode();
        hashCode.Add(obj.SeriesReference);
        hashCode.Add(obj.Period);
        hashCode.Add(obj.DataValue);
        hashCode.Add(obj.Suppressed);
        hashCode.Add((int)obj.Status);
        hashCode.Add((int)obj.Units);
        hashCode.Add(obj.Magnitude);
        hashCode.Add(obj.Subject);
        hashCode.Add(obj.Group);
        hashCode.Add(obj.SeriesTitle1);
        hashCode.Add(obj.SeriesTitle2);
        hashCode.Add(obj.SeriesTitle3);
        hashCode.Add(obj.SeriesTitle4);
        hashCode.Add(obj.SeriesTitle5);
        return hashCode.ToHashCode();
    }
}

internal sealed class YYYYMMParser : ParserBase<byte, DateTime>
{
    public override bool TryParse(ReadOnlySpan<byte> span, out DateTime value)
    {
        if (span.Length == 7 && span[4] == '.')
        {
            var y1 = (uint)span[0] - '0';
            var y2 = (uint)span[1] - '0';
            var y3 = (uint)span[2] - '0';
            var y4 = (uint)span[3] - '0';
            var m1 = (uint)span[5] - '0';
            var m2 = (uint)span[6] - '0';

            if (y1 <= 9 && y2 <= 9 && y3 <= 9 && y4 <= 9 && m1 <= 9 && m2 <= 9)
            {
                value = new(
                    (int)(y1 * 1000 + y2 * 100 + y3 * 10 + y4),
                    (int)(m1 * 10 + m2),
                    1,
                    0,
                    0,
                    0);

                return true;
            }
        }

        value = default;
        return false;
    }
}
