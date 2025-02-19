// ReSharper disable all

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Parallel;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using nietras.SeparatedValues;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser(displayGenColumns: false)]
[HideColumns("Error", "StdDev")]
// [BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class CsvEnumerateBench
{
    private static readonly byte[] _bytes
        = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");
    // = File.ReadAllBytes(@"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Benchmark\Data\65K_Records_Data.csv");

    private static readonly string _chars = Encoding.UTF8.GetString(_bytes);
    private static MemoryStream GetFileStream() => new MemoryStream(_bytes);
    private static readonly ReadOnlySequence<byte> _byteSeq = new(_bytes.AsMemory());
    private static readonly ReadOnlySequence<char> _charSeq = new(_chars.AsMemory());

    private static readonly CsvOptions<byte> _optionsByte = new() { Newline = "\n" };
    private static readonly CsvOptions<char> _optionsChar = new() { Newline = "\n" };

    [Benchmark(Baseline = true)]
    public void Flame_byte()
    {
        foreach (var record in CsvParser.Create(_optionsByte, in _byteSeq))
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                _ = record[i];
            }
        }
    }

    // [Benchmark]
    public void Flame_char()
    {
        foreach (var record in CsvParser.Create(_optionsChar, in _charSeq))
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                _ = record[i];
            }
        }
    }

    // [Benchmark]
    public void Sep_byte()
    {
        var reader = nietras
            .SeparatedValues.Sep.Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false,
                    Unescape = true,
                })
            .From(_bytes);

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i].Span;
            }
        }
    }

    [Benchmark]
    public void Sep_char()
    {
        var reader = nietras
            .SeparatedValues.Sep.Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false,
                    Unescape = true,
                })
            .FromText(_chars);

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i].Span;
            }
        }
    }

    // [Benchmark]
    public void Flame_Parallel()
    {
        CsvParallelReader.Enumerate<object?, Invoker>(in _byteSeq, new()).ForAll(_ => { });
    }

    [Benchmark]
    public async Task Flame_byte_async()
    {
        await foreach (var record in CsvParser.Create(_optionsByte, in _byteSeq))
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                _ = record[i];
            }
        }
    }

    private readonly struct Invoker : ICsvParallelTryInvoke<byte, object?>
    {
        public bool TryInvoke<TRecord>(
            scoped ref TRecord record,
            in CsvParallelState state,
            [MaybeNullWhen(false)] out object? result) where TRecord : ICsvFields<byte>, allows ref struct
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                _ = record[i];
            }

            result = default;
            return false;
        }
    }
}
