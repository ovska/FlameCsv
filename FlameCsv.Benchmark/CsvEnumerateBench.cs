// ReSharper disable all

using System.Buffers;
using System.Diagnostics;
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
    private static readonly byte[] _bytes
        = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");

    private static readonly string _chars = Encoding.UTF8.GetString(_bytes);
    private static MemoryStream GetFileStream() => new MemoryStream(_bytes);
    private static readonly ReadOnlySequence<byte> _byteSeq = new(_bytes.AsMemory());
    private static readonly ReadOnlySequence<char> _charSeq = new(_chars.AsMemory());

    [Benchmark(Baseline = true)]
    public void Flame_byte()
    {
        Span<byte> unescapeBuffer = stackalloc byte[256];
        using var parser = CsvParser.Create(CsvOptions<byte>.Default);
        parser.Reset(in _byteSeq);

        while (parser.TryReadLine(out var line, isFinalBlock: false))
        {
            var reader = new MetaFieldReader<byte>(in line, unescapeBuffer);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                _ = reader[i];
            }
        }
    }

    [Benchmark]
    public void Flame_char()
    {
        Span<char> unescapeBuffer = stackalloc char[128];
        using var parser = CsvParser.Create(CsvOptions<char>.Default);
        parser.Reset(in _charSeq);

        while (parser.TryReadLine(out var line, isFinalBlock: false))
        {
            var reader = new MetaFieldReader<char>(in line, unescapeBuffer);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                _ = reader[i];
            }
        }
    }

    [Benchmark]
    public void Sep_byte()
    {
        var reader = nietras
            .SeparatedValues.Sep.Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false,
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
                })
            .From(new StringReader(_chars));

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i].Span;
            }
        }
    }
}
