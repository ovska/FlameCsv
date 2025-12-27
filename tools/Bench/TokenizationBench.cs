using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public sealed class DataSet(string path)
{
    public string CharsLF => field ??= File.ReadAllText(path).ReplaceLineEndings("\n");
    public string CharsCRLF => field ??= CharsLF.ReplaceLineEndings("\r\n");
    public byte[] BytesLF => field ??= Encoding.UTF8.GetBytes(CharsLF);
    public byte[] BytesCRLF => field ??= Encoding.UTF8.GetBytes(CharsCRLF);

    public string GetChars(bool crlf) => crlf ? CharsCRLF : CharsLF;

    public byte[] GetBytes(bool crlf) => crlf ? BytesCRLF : BytesLF;
}

[HideColumns("Error", "RatioSD")]
public class TokenizationBench
{
    public enum ParserNewline
    {
        LF,
        LF_With_CRLF,
        CRLF,
    }

    public enum DataSetType
    {
        Unquoted,
        QuotedA,
        QuotedB,
    }

    [Params(
        [ /**/
            false,
            true,
        ]
    )]
    public bool Chars { get; set; }

    [Params(
        [ /**/
            DataSetType.Unquoted,
            DataSetType.QuotedA,
            DataSetType.QuotedB,
        ]
    )]
    public DataSetType Dataset { get; set; }

    [Params(
        [ /**/
            ParserNewline.LF,
            ParserNewline.LF_With_CRLF,
            ParserNewline.CRLF,
        ]
    )]
    public ParserNewline Newline { get; set; }

    public bool DataIsCRLF => Newline == ParserNewline.CRLF;
    public bool TokenizerIsLF => Newline == ParserNewline.LF;

    private static readonly uint[] _fieldBuffer = new uint[24 * 65535];

    private static readonly DataSet _unquoted = new("Comparisons/Data/65K_Records_Data.csv");
    private static readonly DataSet _quotedA = new("SampleCSVFile_556kb_4x.csv");
    private static readonly DataSet _quotedB = new("Comparisons/Data/customers-100000.csv");

    private string CharData => GetDataSet().GetChars(DataIsCRLF);

    private byte[] ByteData => GetDataSet().GetBytes(DataIsCRLF);

    private DataSet GetDataSet() =>
        Dataset switch
        {
            DataSetType.Unquoted => _unquoted,
            DataSetType.QuotedA => _quotedA,
            DataSetType.QuotedB => _quotedB,
            _ => throw new InvalidOperationException(),
        };

    private static readonly CsvOptions<char> _dCharLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<byte> _dByteCRLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    private static readonly CsvOptions<byte> _dByteLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<char> _dCharCRLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    [Benchmark(Baseline = true)]
    public void Simd()
    {
        int c;

        if (Chars)
        {
            c = CharTokenizer(Dataset, Newline).Tokenize(_fieldBuffer, 0, CharData);
        }
        else
        {
            c = ByteTokenizer(Dataset, Newline).Tokenize(_fieldBuffer, 0, ByteData);
        }

        // rb.SetFieldsRead(c);
    }

#if AVX2
    private readonly Avx2Tokenizer<byte, FalseConstant> _avx2Byte = new(_dByteLF);
    private readonly Avx2Tokenizer<char, FalseConstant> _avx2Char = new(_dCharLF);
    private readonly Avx2Tokenizer<byte, TrueConstant> _avx2ByteCRLF = new(_dByteCRLF);
    private readonly Avx2Tokenizer<char, TrueConstant> _avx2CharCRLF = new(_dCharCRLF);

    private CsvTokenizer<char> Avx2Char => TokenizerIsLF ? _avx2Char : _avx2CharCRLF;
    private CsvTokenizer<byte> Avx2Byte => TokenizerIsLF ? _avx2Byte : _avx2ByteCRLF;

    // [Benchmark]
    public void Avx2()
    {
        var dst = _fieldBuffer;

        if (Chars)
        {
            _ = Avx2Char.Tokenize(dst, 0, CharData);
        }
        else
        {
            _ = Avx2Byte.Tokenize(dst, 0, ByteData);
        }
    }
#endif

#if false && NET10_0_OR_GREATER
    private readonly Avx512Tokenizer<byte, FalseConstant, TrueConstant> _avx512Byte = new(_dByteLF);
    private readonly Avx512Tokenizer<char, FalseConstant, TrueConstant> _avx512Char = new(_dCharLF);
    private readonly Avx512Tokenizer<byte, TrueConstant, TrueConstant> _avx512ByteCRLF = new(_dByteCRLF);
    private readonly Avx512Tokenizer<char, TrueConstant, TrueConstant> _avx512CharCRLF = new(_dCharCRLF);

    private CsvTokenizer<char> Avx512Char => TokenizerIsLF ? _avx512Char : _avx512CharCRLF;
    private CsvTokenizer<byte> Avx512Byte => TokenizerIsLF ? _avx512Byte : _avx512ByteCRLF;

    // [Benchmark]
    public void Avx512()
    {
        var dst = _fieldBuffer;

        if (Chars)
        {
            _ = Avx512Char.Tokenize(dst, 0, CharData);
        }
        else
        {
            _ = Avx512Byte.Tokenize(dst, 0, ByteData);
        }
    }
#endif

    private static readonly Dictionary<(bool isLF, bool hasQuotes), CsvOptions<char>> _charOptsCache = [];
    private static readonly Dictionary<(bool isLF, bool hasQuotes), CsvOptions<byte>> _byteOptsCache = [];

    private static CsvTokenizer<char> CharTokenizer(DataSetType type, ParserNewline newline)
    {
        var key = (isLF: newline == ParserNewline.LF, hasQuotes: type != DataSetType.Unquoted);
        if (!_charOptsCache.TryGetValue(key, out var options))
        {
            options = new CsvOptions<char>
            {
                Delimiter = ',',
                Quote = key.hasQuotes ? '"' : null,
                Newline = key.isLF ? CsvNewline.LF : CsvNewline.CRLF,
            };
            _charOptsCache[key] = options;
        }
        return options.GetTokenizers().simd!;
    }

    private static CsvTokenizer<byte> ByteTokenizer(DataSetType type, ParserNewline newline)
    {
        var key = (isLF: newline == ParserNewline.LF, hasQuotes: type != DataSetType.Unquoted);
        if (!_byteOptsCache.TryGetValue(key, out var options))
        {
            options = new CsvOptions<byte>
            {
                Delimiter = ',',
                Quote = key.hasQuotes ? '"' : null,
                Newline = key.isLF ? CsvNewline.LF : CsvNewline.CRLF,
            };
            _byteOptsCache[key] = options;
        }
        return options.GetTokenizers().simd!;
    }
}
