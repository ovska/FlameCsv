using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using FlameCsv.Utilities;

namespace FlameCsv.Benchmark;

/*
| Method               | Numeric | Mean       | Error   | StdDev  | Median     | Allocated |
|--------------------- |-------- |-----------:|--------:|--------:|-----------:|----------:|
| TryFormat_Char       | False   |   723.4 ns | 1.51 ns | 1.26 ns |   723.1 ns |         - |
| TryFormatCache_Char  | False   |   248.3 ns | 0.31 ns | 0.26 ns |   248.3 ns |         - |
| TryFormatManual_Char | False   |   219.2 ns | 0.12 ns | 0.10 ns |   219.2 ns |         - |
| TryFormat_Char       | True    |   309.0 ns | 3.99 ns | 3.74 ns |   307.8 ns |         - |
| TryFormatCache_Char  | True    |   265.3 ns | 0.40 ns | 0.33 ns |   265.2 ns |         - |
| TryFormatManual_Char | True    |   281.9 ns | 5.65 ns | 9.74 ns |   286.5 ns |         - |

| Method               | Numeric | Mean       | Error   | StdDev  | Median     | Allocated |
|--------------------- |-------- |-----------:|--------:|--------:|-----------:|----------:|
| TryFormat_Byte       | False   | 1,277.8 ns | 1.26 ns | 1.12 ns | 1,277.7 ns |         - |
| TryFormatCache_Byte  | False   |   247.7 ns | 2.57 ns | 2.41 ns |   246.2 ns |         - |
| TryFormatManual_Byte | False   |   223.6 ns | 0.42 ns | 0.38 ns |   223.5 ns |         - |
| TryFormat_Byte       | True    |   841.8 ns | 8.57 ns | 8.02 ns |   841.4 ns |         - |
| TryFormatCache_Byte  | True    |   261.9 ns | 0.46 ns | 0.36 ns |   262.0 ns |         - |
| TryFormatManual_Byte | True    |   260.3 ns | 5.17 ns | 9.59 ns |   260.8 ns |         - |

*/

[MemoryDiagnoser]
public class EnumBench
{
    private static readonly TypeCode[] _dof =
    [
        ..Enum.GetValues<TypeCode>(),
        ..Enum.GetValues<TypeCode>(),
        ..Enum.GetValues<TypeCode>()
    ];

    private static readonly char[] _charBuffer = new char[256];
    private static readonly byte[] _byteBuffer = new byte[256];

    [Params(false, true)] public bool Numeric { get; set; }

    [Benchmark]
    public void TryFormat_Char()
    {
        string format = Numeric ? "D" : "G";

        foreach (var value in _dof)
        {
            _ = Enum.TryFormat(value, _charBuffer, out _, format);
        }
    }

    [Benchmark]
    public void TryFormat_Byte()
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

    private FrozenDictionary<TypeCode, string>? _stringNumeric;
    private FrozenDictionary<TypeCode, string>? _stringString;

    [Benchmark]
    public void TryFormatCache_Char()
    {
        string format;
        FrozenDictionary<TypeCode, string> names;

        if (Numeric)
        {
            _stringNumeric ??= EnumCacheText<TypeCode>.GetWriteValues("D", false);
            names = _stringNumeric!;
            format = "D";
        }
        else
        {
            _stringString ??= EnumCacheText<TypeCode>.GetWriteValues("G", true);
            names = _stringString!;
            format = "G";
        }

        foreach (var value in _dof)
        {
            if (names is not null && names.TryGetValue(value, out string? name))
            {
                if (_byteBuffer.Length >= name.Length)
                {
                    name.CopyTo(_charBuffer.AsSpan());
                }

                continue;
            }

            _ = Enum.TryFormat(value, _charBuffer, out _, format);
        }
    }

    private FrozenDictionary<TypeCode, byte[]>? _byteNumeric;
    private FrozenDictionary<TypeCode, byte[]>? _byteString;

    [Benchmark]
    public void TryFormatCache_Byte()
    {
        string format;
        FrozenDictionary<TypeCode, byte[]> names;

        if (Numeric)
        {
            _byteNumeric ??= EnumCacheUtf8<TypeCode>.GetWriteValues("D", false);
            names = _byteNumeric!;
            format = "D";
        }
        else
        {
            _byteString ??= EnumCacheUtf8<TypeCode>.GetWriteValues("G", true);
            names = _byteString!;
            format = "G";
        }

        foreach (var value in _dof)
        {
            if (names is not null && names.TryGetValue(value, out byte[]? name))
            {
                if (_byteBuffer.Length >= name.Length)
                {
                    name.CopyTo(_byteBuffer.AsSpan());
                }

                continue;
            }

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

    [Benchmark]
    public void TryFormatManual_Char()
    {
        if (Numeric)
        {
            foreach (var value in _dof)
            {
                _ = TryFormatManualNum(value, _charBuffer.AsSpan(), out _);
            }
        }
        else
        {
            foreach (var value in _dof)
            {
                _ = TryFormatManualStr(value, _charBuffer.AsSpan(), out _);
            }
        }
    }

    [Benchmark]
    public void TryFormatManual_Byte()
    {
        if (Numeric)
        {
            foreach (var value in _dof)
            {
                _ = TryFormatManualNumByte(value, _byteBuffer.AsSpan(), out _);
            }
        }
        else
        {
            foreach (var value in _dof)
            {
                _ = TryFormatManualStrByte(value, _byteBuffer.AsSpan(), out _);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryFormatManualNum(TypeCode value, Span<char> destination, out int written)
    {
        string? str = value switch
        {
            TypeCode.Empty => "0",
            TypeCode.Object => "1",
            TypeCode.DBNull => "2",
            TypeCode.Boolean => "3",
            TypeCode.Char => "4",
            TypeCode.SByte => "5",
            TypeCode.Byte => "6",
            TypeCode.Int16 => "7",
            TypeCode.UInt16 => "8",
            TypeCode.Int32 => "9",
            TypeCode.UInt32 => "10",
            TypeCode.Int64 => "11",
            TypeCode.UInt64 => "12",
            TypeCode.Single => "13",
            TypeCode.Double => "14",
            TypeCode.Decimal => "15",
            TypeCode.DateTime => "16",
            TypeCode.String => "18",
            _ => null,
        };

        if (str is not null && str.Length <= destination.Length)
        {
            str.CopyTo(destination);
            written = str.Length;
            return true;
        }
        else
        {
            written = 0;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryFormatManualStr(TypeCode value, Span<char> destination, out int written)
    {
        string? str = null;

        if ((int)value < _numNames.Length)
        {
            str = _numNames[(int)value];
        }

        if (str is not null && str.Length <= destination.Length)
        {
            str.CopyTo(destination);
            written = str.Length;
            return true;
        }
        else
        {
            written = 0;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryFormatManualNumByte(TypeCode value, Span<byte> destination, out int written)
    {
        ReadOnlySpan<byte> str = value switch
        {
            TypeCode.Empty => "0"u8,
            TypeCode.Object => "1"u8,
            TypeCode.DBNull => "2"u8,
            TypeCode.Boolean => "3"u8,
            TypeCode.Char => "4"u8,
            TypeCode.SByte => "5"u8,
            TypeCode.Byte => "6"u8,
            TypeCode.Int16 => "7"u8,
            TypeCode.UInt16 => "8"u8,
            TypeCode.Int32 => "9"u8,
            TypeCode.UInt32 => "10"u8,
            TypeCode.Int64 => "11"u8,
            TypeCode.UInt64 => "12"u8,
            TypeCode.Single => "13"u8,
            TypeCode.Double => "14"u8,
            TypeCode.Decimal => "15"u8,
            TypeCode.DateTime => "16"u8,
            TypeCode.String => "18"u8,
            _ => [],
        };

        if (str.Length != 0 && str.Length <= destination.Length)
        {
            str.CopyTo(destination);
            written = str.Length;
            return true;
        }
        else
        {
            written = 0;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryFormatManualStrByte(TypeCode value, Span<byte> destination, out int written)
    {
        byte[]? str = null;

        if ((int)value < _numBytes.Length)
        {
            str = _numBytes[(int)value];
        }

        if (str != null && str.Length <= destination.Length)
        {
            str.CopyTo(destination);
            written = str.Length;
            return true;
        }
        else
        {
            written = 0;
            return false;
        }
    }

    private static readonly string?[] _numNames;
    private static readonly byte[]?[] _numBytes;

    static EnumBench()
    {
        _numNames = new string?[19];
        _numNames[0] = "Empty";
        _numNames[1] = "Object";
        _numNames[2] = "DBNull";
        _numNames[3] = "Boolean";
        _numNames[4] = "Char";
        _numNames[5] = "SByte";
        _numNames[6] = "Byte";
        _numNames[7] = "Int16";
        _numNames[8] = "UInt16";
        _numNames[9] = "Int32";
        _numNames[10] = "UInt32";
        _numNames[11] = "Int64";
        _numNames[12] = "UInt64";
        _numNames[13] = "Single";
        _numNames[14] = "Double";
        _numNames[15] = "Decimal";
        _numNames[16] = "DateTime";
        _numNames[17] = null;
        _numNames[18] = "String";

        _numBytes = new byte[]?[19];
        _numBytes[0] = "Empty"u8.ToArray();
        _numBytes[1] = "Object"u8.ToArray();
        _numBytes[2] = "DBNull"u8.ToArray();
        _numBytes[3] = "Boolean"u8.ToArray();
        _numBytes[4] = "Char"u8.ToArray();
        _numBytes[5] = "SByte"u8.ToArray();
        _numBytes[6] = "Byte"u8.ToArray();
        _numBytes[7] = "Int16"u8.ToArray();
        _numBytes[8] = "UInt16"u8.ToArray();
        _numBytes[9] = "Int32"u8.ToArray();
        _numBytes[10] = "UInt32"u8.ToArray();
        _numBytes[11] = "Int64"u8.ToArray();
        _numBytes[12] = "UInt64"u8.ToArray();
        _numBytes[13] = "Single"u8.ToArray();
        _numBytes[14] = "Double"u8.ToArray();
        _numBytes[15] = "Decimal"u8.ToArray();
        _numBytes[16] = "DateTime"u8.ToArray();
        _numBytes[17] = null;
        _numBytes[18] = "String"u8.ToArray();
    }
}
