using FlameCsv.Attributes;

// ReSharper disable ClassNeverInstantiated.Global

namespace FlameCsv.Benchmark.Comparisons;

[CsvTypeMap<char, Entry>]
internal partial class EntryTypeMap;

[CsvTypeMap<byte, Entry>]
internal partial class EntryTypeMapUtf8;
