using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Converters;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
public partial class EnumParseBench
{
    private static readonly string[] _cStrings = [.. Enum.GetValues<TypeCode>().Select(t => t.ToString("G"))];

    private static readonly byte[][] _bStrings =
    [
        ..Enum.GetValues<TypeCode>().Select(t => Encoding.UTF8.GetBytes(t.ToString("G")))
    ];

    private static readonly string[] _cNums = [.. Enum.GetValues<TypeCode>().Select(t => t.ToString("D"))];

    private static readonly byte[][] _bNums =
    [
        ..Enum.GetValues<TypeCode>().Select(t => Encoding.UTF8.GetBytes(t.ToString("D")))
    ];

    [Params(true, false)] public bool Bytes { get; set; }
    [Params(true, false)] public bool IgnoreCase { get; set; }
    [Params(true, false)] public bool ParseNumbers { get; set; }

    private string[] CharData => ParseNumbers ? _cNums : _cStrings;
    private byte[][] ByteData => ParseNumbers ? _bNums : _bStrings;

    [Benchmark(Baseline = true)]
    public void TryParse()
    {
        if (Bytes)
        {
            bool ignoreCase = IgnoreCase;
            Span<char> buffer = stackalloc char[64];

            foreach (var s in ByteData)
            {
                _ = Encoding.UTF8.TryGetChars(s, buffer, out int written);
                _ = Enum.TryParse<TypeCode>(buffer[..written], ignoreCase, out _);
            }
        }
        else
        {
            bool ignoreCase = IgnoreCase;

            foreach (var s in CharData)
            {
                _ = Enum.TryParse<TypeCode>(s, ignoreCase, out _);
            }
        }
    }

    [Benchmark]
    public void Reflection()
    {
        if (Bytes)
        {
            var converter = IgnoreCase ? _xbic : _xbord;

            foreach (var s in _bStrings)
            {
                _ = converter.TryParse(s, out _);
            }
        }
        else
        {
            var converter = IgnoreCase ? _xcic : _xcord;

            foreach (var s in _cStrings)
            {
                _ = converter.TryParse(s.AsSpan(), out _);
            }
        }
    }

    [Benchmark]
    public void SourceGen()
    {
        if (Bytes)
        {
            var converter = IgnoreCase ? _cbic : _cbord;

            foreach (var s in _bStrings)
            {
                _ = converter.TryParse(s, out _);
            }
        }
        else
        {
            var converter = IgnoreCase ? _ccic : _ccord;

            foreach (var s in _cStrings)
            {
                _ = converter.TryParse(s.AsSpan(), out _);
            }
        }
    }

    private static readonly TypeCodeConverterChar _ccic = new(CsvOptions<char>.Default);
    private static readonly TypeCodeConverterChar _ccord = new(new CsvOptions<char> { IgnoreEnumCase = false });
    private static readonly TypeCodeConverterByte _cbic = new(CsvOptions<byte>.Default);
    private static readonly TypeCodeConverterByte _cbord = new(new CsvOptions<byte> { IgnoreEnumCase = false });

    private static readonly EnumTextConverter<TypeCode> _xcic = new(CsvOptions<char>.Default);
    private static readonly EnumTextConverter<TypeCode> _xcord = new(new CsvOptions<char> { IgnoreEnumCase = false });
    private static readonly EnumUtf8Converter<TypeCode> _xbic = new(CsvOptions<byte>.Default);
    private static readonly EnumUtf8Converter<TypeCode> _xbord = new(new CsvOptions<byte> { IgnoreEnumCase = false });

    [CsvEnumConverter<char, TypeCode>]
    private partial class TypeCodeConverterChar;

    [CsvEnumConverter<byte, TypeCode>]
    private partial class TypeCodeConverterByte;
}

/*
| Method     | Bytes | IgnoreCase | ParseNumbers | Mean      | StdDev    | Ratio |
|----------- |------ |----------- |------------- |----------:|----------:|------:|
| TryParse   | False | False      | False        | 582.33 ns |  3.088 ns |  1.00 |
| Reflection | False | False      | False        | 300.89 ns |  0.340 ns |  0.52 |
| SourceGen  | False | False      | False        |  79.76 ns |  1.273 ns |  0.14 |
|            |       |            |              |           |           |       |
| TryParse   | False | False      | True         | 185.49 ns |  2.101 ns |  1.00 |
| Reflection | False | False      | True         | 304.56 ns |  2.484 ns |  1.64 |
| SourceGen  | False | False      | True         |  78.30 ns |  0.701 ns |  0.42 |
|            |       |            |              |           |           |       |
| TryParse   | False | True       | False        | 661.59 ns |  6.298 ns |  1.00 |
| Reflection | False | True       | False        | 369.34 ns |  3.516 ns |  0.56 |
| SourceGen  | False | True       | False        |  82.75 ns |  1.265 ns |  0.13 |
|            |       |            |              |           |           |       |
| TryParse   | False | True       | True         | 186.26 ns |  1.584 ns |  1.00 |
| Reflection | False | True       | True         | 368.88 ns |  3.205 ns |  1.98 |
| SourceGen  | False | True       | True         |  83.87 ns |  1.198 ns |  0.45 |
|            |       |            |              |           |           |       |
| TryParse   | True  | False      | False        | 726.99 ns | 15.936 ns |  1.00 |
| Reflection | True  | False      | False        | 480.53 ns |  0.941 ns |  0.66 |
| SourceGen  | True  | False      | False        |  73.65 ns |  0.433 ns |  0.10 |
|            |       |            |              |           |           |       |
| TryParse   | True  | False      | True         | 326.83 ns |  0.540 ns |  1.00 |
| Reflection | True  | False      | True         | 485.12 ns |  4.999 ns |  1.48 |
| SourceGen  | True  | False      | True         |  72.26 ns |  0.196 ns |  0.22 |
|            |       |            |              |           |           |       |
| TryParse   | True  | True       | False        | 785.22 ns |  1.791 ns |  1.00 |
| Reflection | True  | True       | False        | 574.11 ns |  6.201 ns |  0.73 |
| SourceGen  | True  | True       | False        |  72.89 ns |  0.869 ns |  0.09 |
|            |       |            |              |           |           |       |
| TryParse   | True  | True       | True         | 327.22 ns |  3.023 ns |  1.00 |
| Reflection | True  | True       | True         | 560.96 ns |  5.796 ns |  1.71 |
| SourceGen  | True  | True       | True         |  71.82 ns |  0.928 ns |  0.22 |
 */
