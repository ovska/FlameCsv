using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Converters;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
public class EnumParseBench
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

    [Params(true, false)] public bool IgnoreCase { get; set; }
    [Params(true, false)] public bool ParseNumbers { get; set; }

    private string[] CharData => ParseNumbers ? _cNums : _cStrings;
    private byte[][] ByteData => ParseNumbers ? _bNums : _bStrings;

    [Benchmark]
    public void TryParse_Char()
    {
        bool ignoreCase = IgnoreCase;

        foreach (var s in CharData)
        {
            _ = Enum.TryParse<TypeCode>(s, ignoreCase, out _);
        }
    }

    [Benchmark]
    public void TryParse_Byte()
    {
        bool ignoreCase = IgnoreCase;
        Span<char> buffer = stackalloc char[64];

        foreach (var s in ByteData)
        {
            _ = Encoding.UTF8.TryGetChars(s, buffer, out int written);
            _ = Enum.TryParse<TypeCode>(buffer[..written], ignoreCase, out _);
        }
    }

    [Benchmark]
    public void Char_Reflection()
    {
        var converter = IgnoreCase ? _xcic : _xcord;

        foreach (var s in _cStrings)
        {
            _ = converter.TryParse(s.AsSpan(), out _);
        }
    }

    [Benchmark]
    public void Byte_Reflection()
    {
        var converter = IgnoreCase ? _xbic : _xbord;

        foreach (var s in _bStrings)
        {
            _ = converter.TryParse(s, out _);
        }
    }

    [Benchmark]
    public void Char_SourceGen()
    {
        var converter = IgnoreCase ? _ccic : _ccord;

        foreach (var s in _cStrings)
        {
            _ = converter.TryParse(s.AsSpan(), out _);
        }
    }

    [Benchmark]
    public void Byte_SourceGen()
    {
        var converter = IgnoreCase ? _cbic : _cbord;

        foreach (var s in _bStrings)
        {
            _ = converter.TryParse(s, out _);
        }
    }

    private static readonly TypeCodeConverterChar _ccic = new(new CsvOptions<char> { IgnoreEnumCase = true });
    private static readonly TypeCodeConverterChar _ccord = new(new CsvOptions<char> { IgnoreEnumCase = false });
    private static readonly TypeCodeConverterByte _cbic = new(new CsvOptions<byte> { IgnoreEnumCase = true });
    private static readonly TypeCodeConverterByte _cbord = new(new CsvOptions<byte> { IgnoreEnumCase = false });

    private static readonly EnumTextConverter<TypeCode> _xcic = new(new CsvOptions<char> { IgnoreEnumCase = true });
    private static readonly EnumTextConverter<TypeCode> _xcord = new(new CsvOptions<char> { IgnoreEnumCase = false });
    private static readonly EnumUtf8Converter<TypeCode> _xbic = new(new CsvOptions<byte> { IgnoreEnumCase = true });
    private static readonly EnumUtf8Converter<TypeCode> _xbord = new(new CsvOptions<byte> { IgnoreEnumCase = false });
}

[CsvEnumConverter<char, TypeCode>]
internal partial class TypeCodeConverterChar;

[CsvEnumConverter<byte, TypeCode>]
internal partial class TypeCodeConverterByte;
