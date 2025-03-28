using System.Text.Unicode;
using FlameCsv.Attributes;
using FlameCsv.Converters;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public partial class EnumFormatBench
{
    private static readonly TypeCode[] _dof =
    [
        ..Enum.GetValues<TypeCode>(), ..Enum.GetValues<TypeCode>(), ..Enum.GetValues<TypeCode>()
    ];

    private static readonly char[] _charBuffer = new char[256];
    private static readonly byte[] _byteBuffer = new byte[256];

    [Params(false, true)] public bool Numeric { get; set; }
    [Params(true, false)] public bool Bytes { get; set; }

    [Benchmark(Baseline = true)]
    public void TryFormat()
    {
        if (Bytes)
        {
            string format = Numeric ? "D" : "G";

            foreach (var value in _dof)
            {
                Utf8.TryWriteInterpolatedStringHandler handler = new(
                    literalLength: 0,
                    formattedCount: 1,
                    destination: _byteBuffer,
                    shouldAppend: out bool shouldAppend);

                if (shouldAppend)
                {
                    // the handler needs to be constructed by hand so we can pass in the dynamic format
                    handler.AppendFormatted(value, format);
                    _ = Utf8.TryWrite(_byteBuffer, ref handler, out _);
                }
            }
        }
        else
        {
            string format = Numeric ? "D" : "G";

            foreach (var value in _dof)
            {
                _ = Enum.TryFormat(value, _charBuffer, out _, format);
            }
        }
    }

    [Benchmark]
    public void Reflection()
    {
        if (Bytes)
        {
            EnumUtf8Converter<TypeCode> converter = Numeric ? _xbnum : _xbstr;

            foreach (var value in _dof)
            {
                _ = converter.TryFormat(_byteBuffer, value, out _);
            }
        }
        else
        {
            EnumTextConverter<TypeCode> converter = Numeric ? _xcnum : _xcstr;

            foreach (var value in _dof)
            {
                _ = converter.TryFormat(_charBuffer, value, out _);
            }
        }
    }

    [Benchmark]
    public void SourceGen()
    {
        if (Bytes)
        {
            TypeCodeConverterByte converter = Numeric ? _cbnum : _cbstr;

            foreach (var value in _dof)
            {
                _ = converter.TryFormat(_byteBuffer, value, out _);
            }
        }
        else
        {
            TypeCodeConverterChar converter = Numeric ? _ccnum : _ccstr;

            foreach (var value in _dof)
            {
                _ = converter.TryFormat(_charBuffer, value, out _);
            }
        }
    }

    private static readonly TypeCodeConverterChar _ccnum = new(new CsvOptions<char> { EnumFormat = "D" });
    private static readonly TypeCodeConverterChar _ccstr = new(new CsvOptions<char> { EnumFormat = "G" });
    private static readonly TypeCodeConverterByte _cbnum = new(new CsvOptions<byte> { EnumFormat = "D" });
    private static readonly TypeCodeConverterByte _cbstr = new(new CsvOptions<byte> { EnumFormat = "G" });

    private static readonly EnumTextConverter<TypeCode> _xcnum = new(new CsvOptions<char> { EnumFormat = "D" });
    private static readonly EnumTextConverter<TypeCode> _xcstr = new(new CsvOptions<char> { EnumFormat = "G" });
    private static readonly EnumUtf8Converter<TypeCode> _xbnum = new(new CsvOptions<byte> { EnumFormat = "D" });
    private static readonly EnumUtf8Converter<TypeCode> _xbstr = new(new CsvOptions<byte> { EnumFormat = "G" });

    [CsvEnumConverter<char, TypeCode>]
    private partial class TypeCodeConverterChar;

    [CsvEnumConverter<byte, TypeCode>]
    private partial class TypeCodeConverterByte;
}

/*
| Method     | Numeric | Bytes | Mean       | StdDev  | Ratio |
|----------- |-------- |------ |-----------:|--------:|------:|
| TryFormat  | False   | False |   724.9 ns | 8.40 ns |  1.00 |
| Reflection | False   | False |   281.4 ns | 4.82 ns |  0.39 |
| SourceGen  | False   | False |   245.8 ns | 1.76 ns |  0.34 |
|            |         |       |            |         |       |
| TryFormat  | False   | True  | 1,297.4 ns | 1.50 ns |  1.00 |
| Reflection | False   | True  |   285.7 ns | 0.20 ns |  0.22 |
| SourceGen  | False   | True  |   255.5 ns | 2.49 ns |  0.20 |
|            |         |       |            |         |       |
| TryFormat  | True    | False |   288.4 ns | 3.31 ns |  1.00 |
| Reflection | True    | False |   302.3 ns | 4.70 ns |  1.05 |
| SourceGen  | True    | False |   165.4 ns | 1.76 ns |  0.57 |
|            |         |       |            |         |       |
| TryFormat  | True    | True  |   863.8 ns | 0.86 ns |  1.00 |
| Reflection | True    | True  |   300.1 ns | 0.60 ns |  0.35 |
| SourceGen  | True    | True  |   160.9 ns | 0.24 ns |  0.19 |
*/
