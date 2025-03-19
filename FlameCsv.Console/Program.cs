// ReSharper disable all

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Enumeration;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using JetBrains.Profiler.Api;

namespace FlameCsv.Console
{
    public static class Program
    {
        private static readonly byte[] _bytes
            = File.ReadAllBytes(
                "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Benchmark/Comparisons/Data/SampleCSVFile_556kb.csv");

        private static readonly byte[] _bytesSmall
            = File.ReadAllBytes(
                "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Benchmark/Comparisons/Data/SampleCSVFile_10records.csv");

        private static readonly string _charsSmall = Encoding.UTF8.GetString(_bytesSmall);

        // private static readonly byte[] _bytes2 = File.ReadAllBytes(
        //     @"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Benchmark\Data\65K_Records_Data.csv");

        private static readonly ReadOnlySequence<byte> _byteSeq = new(_bytes.AsMemory());

        private static readonly CsvOptions<byte> _options = new() { Newline = "\n" };
        private static readonly CsvOptions<char> _optionsC = new() { Newline = "\n" };

        static void Main([NotNull] string[] args)
        {
            bool stop = false;
            int iters = 0;
            int snapshot = 0;

            System.Console.CancelKeyPress += (_, e) => stop = true;

            MemoryProfiler.CollectAllocations(false);

            // MeasureProfiler.StartCollectingData();

            if (FlameCsvGlobalOptions.CachingDisabled) throw new UnreachableException();

            while (!stop)
            {
                if (++iters >= 10_000)
                {
                    System.Console.WriteLine($"Snap {0}", snapshot);
                    MemoryProfiler.GetSnapshot($"snap{snapshot++}");
                    iters = 0;
                }

                foreach (var entry in new CsvValueEnumerable<byte, Entry>(_bytesSmall, new CsvOptions<byte>()))
                {
                    _ = entry;
                }
            }

            // MeasureProfiler.StopCollectingData();

            return;

            for (int x = 0; x < 200; x++)
            {
                if (x == 30) MeasureProfiler.StartCollectingData();

                foreach (var item in CsvReader.Read<Entry>(_bytes, EntryTypeMap.Default, _options))
                {
                    _ = item;
                }

                // CsvWriter.Write(Stream.Null, _entries, EntryTypeMap.Default);
                //
                // foreach (var data in (byte[][])[_bytes, _bytes2])
                // {
                //     foreach (var reader in CsvParser.Create(_options, new ReadOnlySequence<byte>(data)).ParseRecords())
                //     {
                //         for (int i = 0; i < reader.FieldCount; i++)
                //         {
                //             _ = reader[i];
                //         }
                //     }
                // }
            }

#if false
            CsvOptions<char> options = new()
            {
                RecordCallback = (ref readonly CsvRecordCallbackArgs<char> args) =>
                {
                    if (args.IsEmpty)
                    {
                        args.HeaderRead = false;
                    }
                    else if (args.Record[0] == '#')
                    {
                        args.SkipRecord = true;
                    }
                }
            };

            Span<byte> unescapeBuffer = stackalloc byte[256];
            using var parser = CsvParser.Create(_options);

            for (int x = 0; x < 1_000; x++)
            {
                parser.SetData(in _byteSeq);

                if (x == 100) MeasureProfiler.StartCollectingData();

                while (parser.TryReadLine(out var line, isFinalBlock: false))
                {
                    var reader = new MetaFieldReader<byte>(in line, unescapeBuffer);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        _ = reader[i];
                    }
                }
            }

#endif

            MeasureProfiler.StopCollectingData();
        }

#pragma warning disable IL2026
        private static Lazy<Entry[]> _entries = new(
            () => CsvReader
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

        [CsvConverter<char, SpanTextConverter<long>>]
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
