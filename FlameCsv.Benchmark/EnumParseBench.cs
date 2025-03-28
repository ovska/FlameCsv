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

    private const CsvEnumOptions IcOpts = CsvEnumOptions.IgnoreCase | CsvEnumOptions.AllowUndefinedValues;
    private const CsvEnumOptions OrdOpts = CsvEnumOptions.AllowUndefinedValues;

    private static readonly TypeCodeConverterChar _ccic = new(new CsvOptions<char> { EnumOptions = IcOpts });
    private static readonly TypeCodeConverterChar _ccord = new(new CsvOptions<char> { EnumOptions = OrdOpts });
    private static readonly TypeCodeConverterByte _cbic = new(new CsvOptions<byte> { EnumOptions = IcOpts });
    private static readonly TypeCodeConverterByte _cbord = new(new CsvOptions<byte> { EnumOptions = OrdOpts });

    private static readonly EnumTextConverter<TypeCode> _xcic = new(new CsvOptions<char> { EnumOptions = IcOpts });
    private static readonly EnumTextConverter<TypeCode> _xcord = new(new CsvOptions<char> { EnumOptions = OrdOpts });
    private static readonly EnumUtf8Converter<TypeCode> _xbic = new(new CsvOptions<byte> { EnumOptions = IcOpts });
    private static readonly EnumUtf8Converter<TypeCode> _xbord = new(new CsvOptions<byte> { EnumOptions = OrdOpts });

    [CsvEnumConverter<char, TypeCode>]
    private partial class TypeCodeConverterChar;

    [CsvEnumConverter<byte, TypeCode>]
    private partial class TypeCodeConverterByte;
}
