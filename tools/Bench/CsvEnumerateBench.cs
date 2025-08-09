using System.Text;
using FlameCsv.Reading;
using nietras.SeparatedValues;

// ReSharper disable all

namespace FlameCsv.Benchmark;

// [MemoryDiagnoser(displayGenColumns: false)]
[HideColumns("Error", "RatioSD")]
public class CsvEnumerateBench
{
    [Params(
        [ /**/
            // true,
            false,
        ]
    )]
    public bool Quoted { get; set; }

    [Params(
        [ /**/
            // true,
            false,
        ]
    )]
    public bool CRLF { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string path = Quoted ? @"Comparisons/Data/SampleCSVFile_556kb.csv" : @"Comparisons/Data/65K_Records_Data.csv";

        _string = File.ReadAllText(path, Encoding.UTF8).ReplaceLineEndings(CRLF ? "\r\n" : "\n");
        _byteArray = Encoding.UTF8.GetBytes(_string); // can't read the bytes directly as we need to replace line endings
    }

    private byte[] _byteArray = [];
    private string _string = "";

    private static CsvOptions<byte> _optionsByteLF = new() { Newline = CsvNewline.LF };
    private static CsvOptions<char> _optionsCharLF = new() { Newline = CsvNewline.LF };
    private static CsvOptions<byte> _optionsByteCRLF = new() { Newline = CsvNewline.CRLF };
    private static CsvOptions<char> _optionsCharCRLF = new() { Newline = CsvNewline.CRLF };

    private CsvOptions<byte> OptionsByte => CRLF ? _optionsByteCRLF : _optionsByteLF;
    private CsvOptions<char> OptionsChar => CRLF ? _optionsCharCRLF : _optionsCharLF;

    [Benchmark(Baseline = true)]
    public void Flame_byte()
    {
        foreach (var _ in new CsvReader<byte>(OptionsByte, _byteArray).ParseRecords())
        {
            // for (int i = 0; i < record.FieldCount; i++)
            // {
            //     _ = record[i];
            // }
        }
    }

    // [Benchmark]
    public void Flame_char()
    {
        foreach (var record in new CsvReader<char>(OptionsChar, _string.AsMemory()).ParseRecords())
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
        using var reader = Sep.Reader(o =>
                o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false,
                    Unescape = true,
                }
            )
            .From(_byteArray);

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i].Span;
            }
        }
    }

    // [Benchmark]
    public void Sep_char()
    {
        using var reader = Sep.Reader(o =>
                o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false, // omit header overhead
                    Unescape = true,
                }
            )
            .FromText(_string);

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i].Span;
            }
        }
    }

#if BENCH_ASYNC
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
#endif

#if FEATURE_PARALLEL
    [Benchmark]
    public void Flame_Parallel()
    {
        CsvParallel.Enumerate<object?, Invoker>(in _byteSeq, new()).ForAll(_ => { });
    }

    private readonly struct Invoker : ICsvParallelTryInvoke<byte, object?>
    {
        public bool TryInvoke(
            scoped ref CsvFieldsRef<byte> fields,
            in CsvParallelState state,
            [MaybeNullWhen(false)] out object? result
        )
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
