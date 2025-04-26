// ReSharper disable all
using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class TokenizationBench
{
    private readonly Meta[] _metaBuffer = new Meta[16 * 65535];
    private readonly string _data = File.ReadAllText("Comparisons/Data/65K_Records_Data.csv", Encoding.UTF8);

    private static readonly CsvDialect<char> _dialect = new CsvDialect<char>
    {
        Delimiter = ',',
        Quote = '"',
        Escape = '\\',
        Newline = NewlineBuffer<char>.LF,
    };

    private readonly SimdTokenizer<char, NewlineParserOne<char, Vec128Char>, Vec128Char> _t128 = new(_dialect, new('\n'));
    private readonly SimdTokenizer<char, NewlineParserOne<char, Vec256Char>, Vec256Char> _t256 = new(_dialect, new('\n'));
    private readonly SimdTokenizer<char, NewlineParserOne<char, Vec512Char>, Vec512Char> _t512 = new(_dialect, new('\n'));

    [Benchmark]
    public int V128()
    {
        return _t128.Tokenize(_metaBuffer, _data, 0);
    }

    [Benchmark]
    public int V256()
    {
        return _t256.Tokenize(_metaBuffer, _data, 0);
    }

    [Benchmark]
    public int V512()
    {
        return _t512.Tokenize(_metaBuffer, _data, 0);
    }
}
