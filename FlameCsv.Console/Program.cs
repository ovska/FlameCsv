// ReSharper disable all

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Reading;
using JetBrains.Profiler.Api;

namespace FlameCsv.Console
{
    public static class Program
    {
        private static readonly byte[] _bytes
            = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");

        private static readonly byte[] _bytes2 = File.ReadAllBytes(
            @"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Benchmark\Data\65K_Records_Data.csv");

        private static readonly ReadOnlySequence<byte> _byteSeq = new(_bytes.AsMemory());

        private static readonly CsvOptions<byte> _options = new() { Newline = "\r\n" };

        static void Main([NotNull] string[] args)
        {
            Span<byte> buffer = stackalloc byte[256];

            foreach (var a in Enumerable.Repeat(0, 100))
            {
                foreach (var data in (byte[][]) [_bytes, _bytes2])
                {
                    using var parser = CsvParser.Create(_options);
                    parser.SetData(new(data));

                    while (parser.TryReadLine(out var line, isFinalBlock: false))
                    {
                        var reader = new CsvFieldsRef<byte>(in line, buffer);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            _ = reader[i];
                        }
                    }
                }
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

            MeasureProfiler.StopCollectingData();
#endif
        }
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

    class X : Base
    {
        protected override int Id => 123;
    }

    abstract class Base
    {
        protected abstract int Id { get; }

        public Base()
        {
            System.Console.WriteLine(Id);
        }
    }
}
