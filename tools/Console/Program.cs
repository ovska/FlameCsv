#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
#pragma warning disable CS0162 // Unreachable code detected

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters.Formattable;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code

namespace FlameCsv.Console
{
    public static class Program
    {
        private static readonly CsvOptions<byte> _options = new() { Newline = CsvNewline.LF };
        private static readonly CsvOptions<char> _optionsC = new() { Newline = CsvNewline.LF };

        static void Main([NotNull] string[] args)
        {
            FileInfo file = new(
                // @"../../../../Bench/Comparisons/Data/65K_Records_Data.csv"
                @"../../../../Bench/Comparisons/Data/SampleCSVFile_556kb_4x.csv"
            );

            string data = File.ReadAllText(file.FullName);

            for (int i = 0; i < 1000; i++)
            {
                // foreach (var _ in Csv.From(data).Read<Entry>(_optionsC)) { }
                foreach (var r in new CsvReader<char>(_optionsC, data.AsMemory()).ParseRecords())
                {
                    for (int j = 0; j < r.FieldCount; j++)
                    {
                        _ = r[j];
                    }
                }
            }

            JetBrains.Profiler.Api.MeasureProfiler.StartCollectingData();

            for (int i = 0; i < 5000; i++)
            {
                // foreach (var _ in Csv.From(data).Read<Entry>(_optionsC)) { }
                foreach (var r in new CsvReader<char>(_optionsC, data.AsMemory()).ParseRecords())
                {
                    for (int j = 0; j < r.FieldCount; j++)
                    {
                        _ = r[j];
                    }
                }
            }

            JetBrains.Profiler.Api.MeasureProfiler.StopCollectingData();

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
        }

#pragma warning disable IL2026
        private static Lazy<Entry[]> _entries = new(() =>
            Csv.FromFile("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv")
                .Read<Entry>(new() { HasHeader = false })
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

file static class Extensions
{
    [UnsafeAccessor(kind: UnsafeAccessorKind.Field, Name = "_fields")]
    public static extern ref ushort[] GetEolArrayRef(this RecordBuffer buffer);
}
