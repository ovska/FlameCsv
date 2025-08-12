// ReSharper disable all

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
#pragma warning disable CS0162 // Unreachable code detected

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters.Formattable;
using FlameCsv.Intrinsics;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using JetBrains.Profiler.Api;

namespace FlameCsv.Console
{
    public static class Program
    {
        private static readonly CsvOptions<byte> _options = new() { Newline = CsvNewline.LF };
        private static readonly CsvOptions<char> _optionsC = new() { Newline = CsvNewline.LF };

        static void Main([NotNull] string[] args)
        {
            FileInfo file = new(
                @"C:\Users\Sipi\source\repos\FlameCsv\tools\Bench\Comparisons\Data\65K_Records_Data.csv"
            // @"C:\Users\Sipi\source\repos\FlameCsv\tools\Bench\Comparisons\Data\SampleCSVFile_556kb_4x.csv"
            );

            byte[] byteArray = File.ReadAllBytes(file.FullName);
            // var metas = new Reading.Internal.Meta[65536];

            {
                var rb = new RecordBuffer();
                rb.GetFieldArrayRef() = new uint[24 * 65535];
                rb.GetQuoteArrayRef() = new byte[24 * 65535];
                rb.GetEolArrayRef() = new int[65535 + 256];

                // new SimdTokenizer<byte, NewlineCRLF>(CsvOptions<byte>.Default)
                //     .Tokenize(rb, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(byteArray).ReplaceLineEndings("\r\n")));
                new Avx2Tokenizer<byte, NewlineLF>(CsvOptions<byte>.Default).Tokenize(rb, byteArray);
            }

            // var tokenizer = new SimdTokenizer<byte, NewlineLF, Vec256>(CsvOptions<byte>.Default);
            // _ = tokenizer.Tokenize(metas, byteArray, 0);

            // MemoryProfiler.CollectAllocations(true);
            // MeasureProfiler.StartCollectingData();

            for (int i = 0; i < 10_000; i++)
            {
                if (i == 5_000)
                {
                    MeasureProfiler.StartCollectingData();
                }

                using var parser = new CsvReader<byte>(_options, CsvBufferReader.Create(byteArray));

                foreach (var r in parser.ParseRecords())
                {
                    IterateFields(in r);
                }
            }

            MeasureProfiler.StopCollectingData();

            [MethodImpl(MethodImplOptions.NoInlining)]
            static void IterateFields(ref readonly CsvRecordRef<byte> record)
            {
                for (int i = 0; i < record.FieldCount; i++)
                {
                    _ = record[i];
                }
            }

            /*
1   20161
2   29957
3   46465
4   104816
5   33603
6   134
7   0

1   8814
2   6534
3   8482
4   10145
5   7989
6   519
0
0
            */

            _ = 1;
        }

#pragma warning disable IL2026
        private static Lazy<Entry[]> _entries = new(() =>
            CsvReader
                .Read<Entry>(
                    File.ReadAllBytes(
                        "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv"
                    ),
                    new() { HasHeader = false }
                )
                .ToArray()
        );
#pragma warning restore IL2026
    }

    [CsvTypeMap<char, Obj>]
    partial class ObjTypeMap;

    public class Obj
    {
        public DayOfWeek DOF { get; set; }
        public int Id { get; set; }

        [CsvStringPooling]
        public string? Name { get; set; }

        [CsvHeader("Enabled")]
        public bool IsEnabled { get; set; }

        [CsvConverter<SpanTextConverter<long>>]
        public long? Age { get; set; }
    }

    class MyConverter : CsvConverter<char, int>
    {
        public override bool TryFormat(Span<char> destination, int value, out int charsWritten)
        {
            throw new System.NotImplementedException();
        }

        public override bool TryParse(ReadOnlySpan<char> source, out int value)
        {
            throw new System.NotImplementedException();
        }
    }

    public sealed class Entry
    {
        [CsvIndex(0)]
        public int Index { get; set; }

        [CsvIndex(1)]
        public string? Name { get; set; }

        [CsvIndex(2)]
        public string? Contact { get; set; }

        [CsvIndex(3)]
        public int Count { get; set; }

        [CsvIndex(4)]
        public double Latitude { get; set; }

        [CsvIndex(5)]
        public double Longitude { get; set; }

        [CsvIndex(6)]
        public double Height { get; set; }

        [CsvIndex(7)]
        public string? Location { get; set; }

        [CsvIndex(8)]
        public string? Category { get; set; }

        [CsvIndex(9)]
        public double? Popularity { get; set; }
    }

    [CsvTypeMap<byte, Entry>]
    public partial class EntryTypeMap;

    [CsvTypeMap<char, Entry>]
    public partial class EntryTypeMapText;
}
