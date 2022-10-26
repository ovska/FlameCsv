using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using FlameCsv;
using FlameCsv.Binding;
using FlameCsv.Binding.Providers;
using FlameCsv.Readers;

var xyz = Enumerable.Range(0, byte.MaxValue)
    .Select(i => (char)i)
    .Where(i => char.IsWhiteSpace((char)i))
    .ToArray();

var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = Environment.NewLine,
    HasHeaderRecord = false,
};

List<Item> records;
using (var reader = new StreamReader("/home/sipi/test.csv"))
using (var csv = new CsvReader(reader, config))
{
    records = csv.GetRecords<Item>().ToList();
    Debugger.Break();
}

///////////////////////////////////////////////////////////////////////7

var bindings = new CsvBinding[]
{
    new(0, typeof(Item).GetProperty(nameof(Item.SeriesReference))!),
    new(1, typeof(Item).GetProperty(nameof(Item.Period))!),
    new(2, typeof(Item).GetProperty(nameof(Item.DataValue))!),
    new(3, typeof(Item).GetProperty(nameof(Item.Suppressed))!),
    new(4, typeof(Item).GetProperty(nameof(Item.Status))!),
    new(5, typeof(Item).GetProperty(nameof(Item.Units))!),
    new(6, typeof(Item).GetProperty(nameof(Item.Magnitude))!),
    new(7, typeof(Item).GetProperty(nameof(Item.Subject))!),
    new(8, typeof(Item).GetProperty(nameof(Item.Group))!),
    new(9, typeof(Item).GetProperty(nameof(Item.SeriesTitle1))!),
    new(10, typeof(Item).GetProperty(nameof(Item.SeriesTitle2))!),
    new(11, typeof(Item).GetProperty(nameof(Item.SeriesTitle3))!),
    new(12, typeof(Item).GetProperty(nameof(Item.SeriesTitle4))!),
    new(13, typeof(Item).GetProperty(nameof(Item.SeriesTitle5))!),
};

await using var stream = File.OpenRead("/home/sipi/test.csv");


var collection = new ManualBindingProvider<byte, Item>(bindings);
var config_ = CsvConfiguration<byte>.DefaultBuilder.SetBinder(collection).Build();
var reader_ = new CsvStreamReader<Item>(config_);
var result = new List<Item>();
await foreach (var item in reader_.ReadAsync(stream))
{
    result.Add(item);
}

Debugger.Break();
try
{
    var _collection = new ManualBindingProvider<char, Item>(bindings);
    var _config_ = CsvConfiguration<char>.DefaultBuilder.SetBinder(_collection).Build();
    
    using (var reader = new StreamReader("/home/sipi/test.csv"))
    using (var csv = new CsvTextReader<Item>(reader, _config_))
    {
        var result__ = new List<Item>();
        await foreach (var item in csv.ReadAsync())
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
    public string? SeriesReference { get; set; }

    [CsvHelper.Configuration.Attributes.Index(1)]
    public string? Period { get; set; } // date

    [CsvHelper.Configuration.Attributes.Index(2)]
    public int DataValue { get; set; }

    [CsvHelper.Configuration.Attributes.Index(3)]
    public string? Suppressed { get; set; }

    [CsvHelper.Configuration.Attributes.Index(4)]
    public StatusEnum Status { get; set; }

    [CsvHelper.Configuration.Attributes.Index(5)]
    public UnitsEnum Units { get; set; }

    [CsvHelper.Configuration.Attributes.Index(6)]
    public int Magnitude { get; set; }

    [CsvHelper.Configuration.Attributes.Index(7)]
    public string? Subject { get; set; }

    [CsvHelper.Configuration.Attributes.Index(8)]
    public string? Group { get; set; }

    [CsvHelper.Configuration.Attributes.Index(9)]
    public string? SeriesTitle1 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(10)]
    public string? SeriesTitle2 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(11)]
    public string? SeriesTitle3 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(12)]
    public string? SeriesTitle4 { get; set; }

    [CsvHelper.Configuration.Attributes.Index(13)]
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
