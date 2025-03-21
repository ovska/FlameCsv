﻿using FlameCsv.Attributes;

namespace FlameCsv.Benchmark.Comparisons;

public sealed class Entry
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
