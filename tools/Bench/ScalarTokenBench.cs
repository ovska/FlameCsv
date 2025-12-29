using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class ScalarTokenBench
{
    [Params(true, false)]
    public bool Chars { get; set; }

    private static readonly uint[] _fieldBuffer = new uint[24 * 65535];
    private static readonly string _chars0LF = File.ReadAllText("Comparisons/Data/65K_Records_Data.csv")
        .ReplaceLineEndings("\n");
    private static readonly string _chars1LF = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv")
        .ReplaceLineEndings("\n");
    private static readonly byte[] _bytes0LF = Encoding.UTF8.GetBytes(_chars0LF);
    private static readonly byte[] _bytes1LF = Encoding.UTF8.GetBytes(_chars1LF);

    private readonly ScalarTokenizer<char, FalseConstant, TrueConstant> _withQuote = new(
        new CsvOptions<char> { Newline = CsvNewline.LF }
    );
    private readonly ScalarTokenizer<char, FalseConstant, FalseConstant> _withoutQuote = new(
        new CsvOptions<char> { Newline = CsvNewline.LF, Quote = null }
    );
    private readonly ScalarTokenizer<byte, FalseConstant, TrueConstant> _withQuoteByte = new(
        new CsvOptions<byte> { Newline = CsvNewline.LF }
    );
    private readonly ScalarTokenizer<byte, FalseConstant, FalseConstant> _withoutQuoteByte = new(
        new CsvOptions<byte> { Newline = CsvNewline.LF, Quote = null }
    );

    [Benchmark]
    public void Quot()
    {
        if (Chars)
        {
            _ = _withQuote.Tokenize(_fieldBuffer, 0, _chars1LF, false);
        }
        else
        {
            _ = _withQuoteByte.Tokenize(_fieldBuffer, 0, _bytes1LF, false);
        }
    }

    [Benchmark]
    public void NoQuot()
    {
        if (Chars)
        {
            _ = _withoutQuote.Tokenize(_fieldBuffer, 0, _chars0LF, false);
        }
        else
        {
            _ = _withoutQuoteByte.Tokenize(_fieldBuffer, 0, _bytes0LF, false);
        }
    }
}

/*
| Method | Mean       | StdDev   |
|------- |-----------:|---------:|
| Quot   |   551.9 us | 21.29 us |
| NoQuot | 3,388.9 us | 19.91 us |

| Method | Mean       | StdDev   |
|------- |-----------:|---------:|
| Quot   |   536.6 us | 19.18 us |
| NoQuot | 3,270.0 us | 13.85 us |

CHARS
| Method | Mean       | StdDev   |
|------- |-----------:|---------:|
| Quot   |   643.2 us |  4.49 us |
| NoQuot | 3,975.5 us | 27.02 us |

|------- |------ |-----------:|---------:|
| Quot   | False |   516.0 us | 18.17 us |
| NoQuot | False | 3,241.4 us | 12.75 us |
| Quot   | True  |   568.5 us |  2.79 us |
| NoQuot | True  | 3,796.0 us | 12.02 us |
*/
