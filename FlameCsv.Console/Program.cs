// ReSharper disable all

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
#pragma warning disable CS0162 // Unreachable code detected

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Enumeration;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using JetBrains.Profiler.Api;

namespace FlameCsv.Console
{
    public static class Program
    {
        private static readonly CsvOptions<byte> _options = new() { Newline = "\n" };
        private static readonly CsvOptions<char> _optionsC = new() { Newline = "\n" };

        static async Task Main([NotNull] string[] args)
        {
            FileInfo file = new(
                @"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Benchmark\Comparisons\Data\65K_Records_Data.csv");

            using var mmf = MemoryMappedFile.CreateFromFile(file.FullName, FileMode.Open);

            // MemoryProfiler.CollectAllocations(true);
            // MeasureProfiler.StartCollectingData();

            for (int i = 0; i < 100; i++)
            {
                using var stream = mmf.CreateViewStream(0, size: file.Length, MemoryMappedFileAccess.Read);

                if (i == 10)
                {
                    MeasureProfiler.StartCollectingData();
                }

                var parser = new CsvReader<byte>(
                    _options,
                    CsvBufferReader.Create(
                        stream,
                        MemoryPool<byte>.Shared));
                // var parser = CsvParser.Create(_options, new PipeReaderWrapper(PipeReader.Create(stream, new(bufferSize: 1024*16))));

                await foreach (var r in parser.ParseRecordsAsync())
                {
                    _ = r;
                }
            }

            // MemoryProfiler.CollectAllocations(false);
            MeasureProfiler.StopCollectingData();
        }

#pragma warning disable IL2026
        private static Lazy<Entry[]> _entries = new(() => CsvReader
            .Read<Entry>(
                File.ReadAllBytes(
                    "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv"),
                new() { HasHeader = false })
            .ToArray());
#pragma warning restore IL2026
    }

    [CsvTypeMap<char, Obj>(ThrowOnDuplicate = false, IgnoreUnmatched = true)]
    partial class ObjTypeMap;

    public class Obj
    {
        public DayOfWeek DOF { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
        [CsvHeader("Enabled")] public bool IsEnabled { get; set; }

        [CsvConverter<SpanTextConverter<long>>]
        public long? Age { get; set; }
    }

    public sealed class Entry
    {
        [CsvIndex(0)] public int Index { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
        [CsvIndex(2)] public string? Contact { get; set; }
        [CsvIndex(3)] public int Count { get; set; }
        [CsvIndex(4)] public double Latitude { get; set; }
        [CsvIndex(5)] public double Longitude { get; set; }
        [CsvIndex(6)] public double Height { get; set; }
        [CsvIndex(7)] public string? Location { get; set; }
        [CsvIndex(8)] public string? Category { get; set; }
        [CsvIndex(9)] public double? Popularity { get; set; }
    }

    [CsvTypeMap<byte, Entry>]
    public partial class EntryTypeMap;

    [CsvTypeMap<char, Entry>]
    public partial class EntryTypeMapText;
}
