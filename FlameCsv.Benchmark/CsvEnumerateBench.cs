// ReSharper disable all

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using nietras.SeparatedValues;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser(displayGenColumns: false)]
[HideColumns("Error", "StdDev")]
// [BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class CsvEnumerateBench
{
    [Params(true, false)] public bool AltData { get; set; }
    [Params(true, false)] public bool CRLF { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string path = AltData
            ? @"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Benchmark\Data\65K_Records_Data.csv"
            : @"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Tests\TestData\SampleCSVFile_556kb.csv";

        _string = File
            .ReadAllText(path, Encoding.UTF8)
            .ReplaceLineEndings(CRLF ? "\r\n" : "\n");

        _byteArray = Encoding.UTF8.GetBytes(_string);
        _byteSeq = new ReadOnlySequence<byte>(_byteArray);
        _charSeq = new ReadOnlySequence<char>(_string.AsMemory());

        _optionsByte = new() { Newline = CRLF ? "\r\n" : "\n" };
        _optionsChar = new() { Newline = CRLF ? "\r\n" : "\n" };
    }

    private byte[] _byteArray = [];
    private string _string = "";
    private ReadOnlySequence<byte> _byteSeq;
    private ReadOnlySequence<char> _charSeq;
    private CsvOptions<byte> _optionsByte = null!;
    private CsvOptions<char> _optionsChar = null!;

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

    [Benchmark]
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

    [Benchmark]
    public void Sep_byte()
    {
        using var reader = nietras
            .SeparatedValues.Sep.Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false,
                    Unescape = true,
                })
            .From(_byteArray);

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
        using var reader = nietras
            .SeparatedValues.Sep.Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false,
                    Unescape = true,
                })
            .FromText(_string);

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i].Span;
            }
        }
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

#if FEATURE_PARALLEL
    [Benchmark]
    public void Flame_Parallel()
    {
        CsvParallel.Enumerate<object?, Invoker>(in _byteSeq, new()).ForAll(_ => { });
    }

    private readonly struct Invoker : ICsvParallelTryInvoke<byte, object?>
    {
        public bool TryInvoke(scoped ref CsvFieldsRef<byte> fields, in CsvParallelState state, [MaybeNullWhen(false)] out object? result)
        {
            for (int i = 0; i < fields.FieldCount; i++)
            {
                _ = fields[i];
            }

            result = default;
            return false;
        }
    }
#endif
}
