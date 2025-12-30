using System.ComponentModel.DataAnnotations;
using FlameCsv.Attributes;

namespace FlameCsv.Benchmark.Comparisons;

public sealed class Entry
{
    [CsvHelper.Configuration.Attributes.Index(0), Required, CsvRequired, CsvIndex(0)]
    public int Index { get; set; }

    [CsvHelper.Configuration.Attributes.Index(1), Required, CsvRequired, CsvIndex(1)]
    public string? Name { get; set; }

    [CsvHelper.Configuration.Attributes.Index(2), Required, CsvRequired, CsvIndex(2)]
    public string? Contact { get; set; }

    [CsvHelper.Configuration.Attributes.Index(3), Required, CsvRequired, CsvIndex(3)]
    public int Count { get; set; }

    [CsvHelper.Configuration.Attributes.Index(4), Required, CsvRequired, CsvIndex(4)]
    public double Latitude { get; set; }

    [CsvHelper.Configuration.Attributes.Index(5), Required, CsvRequired, CsvIndex(5)]
    public double Longitude { get; set; }

    [CsvHelper.Configuration.Attributes.Index(6), Required, CsvRequired, CsvIndex(6)]
    public double Height { get; set; }

    [CsvHelper.Configuration.Attributes.Index(7), Required, CsvRequired, CsvIndex(7)]
    public string? Location { get; set; }

    [CsvHelper.Configuration.Attributes.Index(8), Required, CsvRequired, CsvIndex(8)]
    public string? Category { get; set; }

    [CsvHelper.Configuration.Attributes.Index(9), Required, CsvRequired, CsvIndex(9)]
    public double? Popularity { get; set; }
}

[CsvTypeMap<char, Entry>]
internal partial class EntryTypeMap;

[CsvTypeMap<byte, Entry>]
internal partial class EntryTypeMapUtf8;
