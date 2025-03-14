using System.Buffers;
using System.Text;
using FlameCsv.Reading;
using nietras.SeparatedValues;

// ReSharper disable all

namespace FlameCsv.Benchmark;

[MemoryDiagnoser(displayGenColumns: false)]
[HideColumns("Error", "RatioSD")]
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

        _optionsByteLF = new() { Newline = "\n" };
        _optionsCharLF = new() { Newline = "\n" };
        _optionsByteCRLF = new() { Newline = "\r\n" };
        _optionsCharCRLF = new() { Newline = "\r\n" };
    }

    private byte[] _byteArray = [];
    private string _string = "";
    private ReadOnlySequence<byte> _byteSeq;
    private ReadOnlySequence<char> _charSeq;

    private CsvOptions<byte> _optionsByteLF = null!;
    private CsvOptions<char> _optionsCharLF = null!;
    private CsvOptions<byte> _optionsByteCRLF = null!;
    private CsvOptions<char> _optionsCharCRLF = null!;

    private CsvOptions<byte> OptionsByte => CRLF ? _optionsByteCRLF : _optionsByteLF;
    private CsvOptions<char> OptionsChar => CRLF ? _optionsCharCRLF : _optionsCharLF;

    [Benchmark(Baseline = true)]
    public void Flame_byte()
    {
        foreach (var record in CsvParser.Create(OptionsByte, in _byteSeq).ParseRecords())
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
        foreach (var record in CsvParser.Create(OptionsChar, in _charSeq).ParseRecords())
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
        using var reader = Sep.Reader(
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
        using var reader = Sep.Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false, // omit header overhead
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
        await foreach (var record in CsvParser.Create(OptionsByte, in _byteSeq).ParseRecordsAsync())
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                _ = record[i];
            }
        }
    }

    [Benchmark]
    public async Task Flame_char_async()
    {
        await foreach (var record in CsvParser.Create(OptionsChar, in _charSeq).ParseRecordsAsync())
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
