using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Utilities;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
public class EnumParseBench
{
    static EnumParseBench()
    {
        _cOrdinal = EnumCacheText<TypeCode>.GetReadValues(false, false);
        _cIgnoreCase = EnumCacheText<TypeCode>.GetReadValues(true, false);
        _bOrdinal = EnumCacheUtf8<TypeCode>.GetReadValues(false, false);
        _bIgnoreCase = EnumCacheUtf8<TypeCode>.GetReadValues(true, false);
    }

    private static readonly FrozenDictionary<string, TypeCode>.AlternateLookup<ReadOnlySpan<char>> _cOrdinal;
    private static readonly FrozenDictionary<string, TypeCode>.AlternateLookup<ReadOnlySpan<char>> _cIgnoreCase;
    private static readonly FrozenDictionary<StringLike, TypeCode>.AlternateLookup<ReadOnlySpan<byte>> _bOrdinal;
    private static readonly FrozenDictionary<StringLike, TypeCode>.AlternateLookup<ReadOnlySpan<byte>> _bIgnoreCase;

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

    // [Benchmark]
    // public void Char_Strings()
    // {
    //     bool ignoreCase = IgnoreCase;

    //     foreach (var s in _cStrings)
    //     {
    //         _ = Enum.TryParse<TypeCode>(s, ignoreCase, out _);
    //     }
    // }

    // [Benchmark]
    // public void Byte_Strings()
    // {
    //     bool ignoreCase = IgnoreCase;
    //     Span<char> buffer = stackalloc char[32];

    //     foreach (var s in _bStrings)
    //     {
    //         _ = Encoding.UTF8.TryGetChars(s, buffer, out int written);
    //         _ = Enum.TryParse<TypeCode>(buffer[..written], ignoreCase, out _);
    //     }
    // }

    // [Benchmark]
    // public void Char_Nums()
    // {
    //     bool ignoreCase = IgnoreCase;

    //     foreach (var s in _cNums)
    //     {
    //         _ = Enum.TryParse<TypeCode>(s, ignoreCase, out _);
    //     }
    // }

    // [Benchmark]
    // public void Byte_Nums()
    // {
    //     bool ignoreCase = IgnoreCase;
    //     Span<char> buffer = stackalloc char[32];

    //     foreach (var s in _bNums)
    //     {
    //         _ = Encoding.UTF8.TryGetChars(s, buffer, out int written);
    //         _ = Enum.TryParse<TypeCode>(buffer[..written], ignoreCase, out _);
    //     }
    // }

    // [Benchmark]
    // public void Char_Cached_num()
    // {
    //     var lookup = IgnoreCase ? _cIgnoreCase : _cOrdinal;

    //     foreach (var s in _cNums)
    //     {
    //         _ = lookup.TryGetValue(s.AsSpan(), out _);
    //     }
    // }

    // [Benchmark]
    // public void Byte_Cached_num()
    // {
    //     var lookup = IgnoreCase ? _bIgnoreCase : _bOrdinal;

    //     foreach (var s in _bNums)
    //     {
    //         _ = lookup.TryGetValue(s, out _);
    //     }
    // }

    // [Benchmark]
    // public void Char_Cached()
    // {
    //     var lookup = IgnoreCase ? _cIgnoreCase : _cOrdinal;

    //     foreach (var s in _cStrings)
    //     {
    //         _ = lookup.TryGetValue(s.AsSpan(), out _);
    //     }
    // }

    // [Benchmark]
    // public void Byte_Cached()
    // {
    //     var lookup = IgnoreCase ? _bIgnoreCase : _bOrdinal;

    //     foreach (var s in _bStrings)
    //     {
    //         _ = lookup.TryGetValue(s, out _);
    //     }
    // }

    // [Benchmark]
    // public void Char_Cached_Small_num()
    // {
    //     var lookup = IgnoreCase ? _cIgnoreCase : _cOrdinal;

    //     foreach (var s in _cNums)
    //     {
    //         if (EnumMemberCache<char, TypeCode>.TryGetFast(s.AsSpan(), out _))
    //         {
    //             continue;
    //         }

    //         _ = lookup.TryGetValue(s.AsSpan(), out _);
    //     }
    // }

    // [Benchmark]
    // public void Byte_Cached_Small_num()
    // {
    //     var lookup = IgnoreCase ? _bIgnoreCase : _bOrdinal;

    //     foreach (var s in _bNums)
    //     {
    //         if (EnumMemberCache<byte, TypeCode>.TryGetFast(s, out _))
    //         {
    //             continue;
    //         }

    //         _ = lookup.TryGetValue(s, out _);
    //     }
    // }

    // [Benchmark]
    // public void Char_Cached_Small()
    // {
    //     var lookup = IgnoreCase ? _cIgnoreCase : _cOrdinal;

    //     foreach (var s in _cStrings)
    //     {
    //         if (EnumMemberCache<char, TypeCode>.TryGetFast(s.AsSpan(), out _))
    //         {
    //             continue;
    //         }

    //         _ = lookup.TryGetValue(s.AsSpan(), out _);
    //     }
    // }

    // [Benchmark]
    // public void Byte_Cached_Small()
    // {
    //     var lookup = IgnoreCase ? _bIgnoreCase : _bOrdinal;

    //     foreach (var s in _bStrings)
    //     {
    //         if (EnumMemberCache<byte, TypeCode>.TryGetFast(s, out _))
    //         {
    //             continue;
    //         }

    //         _ = lookup.TryGetValue(s, out _);
    //     }
    // }

    [Benchmark]
    public void Manual()
    {
        if (IgnoreCase)
        {
            foreach (var s in _cStrings)
            {
                _ = TryParseIgnoreCase(s.AsSpan(), out _);
            }
        }
        else
        {
            foreach (var s in _cStrings)
            {
                _ = TryParse(s.AsSpan(), out _);
            }
        }
    }

    private static bool TryParse(ReadOnlySpan<char> value, out TypeCode typeCode)
    {
        /*
Boolean
Byte
Char
DateTime
DBNull
Decimal
Double
Empty
Int16
Int32
Int64
Object
SByte
Single
String
UInt16
UInt32
UInt64
        */

        if (value.IsEmpty)
        {
            goto Fail;
        }

        switch (value[0])
        {
            case 'B':
                if (value.Length == 7 && value.EndsWith("oolean"))
                {
                    typeCode = TypeCode.Boolean;
                    return true;
                }
                if (value.Length == 4 && value.EndsWith("yte"))
                {
                    typeCode = TypeCode.Byte;
                    return true;
                }
                break;
            case 'C':

                if (value.Length == 4 && value.EndsWith("har"))
                {
                    typeCode = TypeCode.Char;
                    return true;
                }
                break;
            case 'D':
                if (value.Length == 8 && value.EndsWith("ateTime"))
                {
                    typeCode = TypeCode.DateTime;
                    return true;
                }
                if (value.Length == 5 && value.EndsWith("DBNull"))
                {
                    typeCode = TypeCode.DBNull;
                    return true;
                }
                if (value.Length == 7 && value.EndsWith("ecimal"))
                {
                    typeCode = TypeCode.Decimal;
                    return true;
                }
                break;
            case 'E':
                if (value.Length == 5 && value.EndsWith("mpty"))
                {
                    typeCode = TypeCode.Empty;
                    return true;
                }
                break;
            case 'I':
                if (value.Length == 5)
                {
                    if (value.EndsWith("nt16"))
                    {
                        typeCode = TypeCode.Int16;
                        return true;
                    }
                    if (value.EndsWith("nt32"))
                    {
                        typeCode = TypeCode.Int32;
                        return true;
                    }
                    if (value.EndsWith("nt64"))
                    {
                        typeCode = TypeCode.Int64;
                        return true;
                    }
                }
                break;
            case 'O':
                if (value.Length == 6 && value.EndsWith("bject"))
                {
                    typeCode = TypeCode.Object;
                    return true;
                }
                break;
            case 'S':
                if (value.Length == 4 && value.EndsWith("Byte"))
                {
                    typeCode = TypeCode.SByte;
                    return true;
                }
                if (value.Length == 6)
                {
                    if (value.EndsWith("ingle"))
                    {
                        typeCode = TypeCode.Single;
                        return true;
                    }
                    if (value.EndsWith("tring"))
                    {
                        typeCode = TypeCode.String;
                        return true;
                    }
                }
                break;
            case 'U':
                if (value.Length == 6)
                {
                    if (value.EndsWith("Int16"))
                    {
                        typeCode = TypeCode.UInt16;
                        return true;
                    }
                    if (value.EndsWith("Int32"))
                    {
                        typeCode = TypeCode.UInt32;
                        return true;
                    }
                    if (value.EndsWith("Int64"))
                    {
                        typeCode = TypeCode.UInt64;
                        return true;
                    }
                }
                break;
            default:
                break;
        }

        Fail:
        Unsafe.SkipInit(out typeCode);
        return false;
    }

    private static bool TryParseIgnoreCase(ReadOnlySpan<char> value, out TypeCode typeCode)
    {
        /*
Boolean
Byte
Char
DateTime
DBNull
Decimal
Double
Empty
Int16
Int32
Int64
Object
SByte
Single
String
UInt16
UInt32
UInt64
        */

        if (value.IsEmpty)
        {
            goto Fail;
        }

        switch (value[0])
        {
            case 'B':
                if (value.Length == 7 && value.EndsWith("oolean", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.Boolean;
                    return true;
                }
                if (value.Length == 4 && value.EndsWith("yte", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.Byte;
                    return true;
                }
                break;
            case 'C':

                if (value.Length == 4 && value.EndsWith("har", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.Char;
                    return true;
                }
                break;
            case 'D':
                if (value.Length == 8 && value.EndsWith("ateTime", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.DateTime;
                    return true;
                }
                if (value.Length == 5 && value.EndsWith("DBNull", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.DBNull;
                    return true;
                }
                if (value.Length == 7 && value.EndsWith("ecimal", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.Decimal;
                    return true;
                }
                break;
            case 'E':
                if (value.Length == 5 && value.EndsWith("mpty", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.Empty;
                    return true;
                }
                break;
            case 'I':
                if (value.Length == 5)
                {
                    if (value.EndsWith("nt16", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.Int16;
                        return true;
                    }
                    if (value.EndsWith("nt32", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.Int32;
                        return true;
                    }
                    if (value.EndsWith("nt64", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.Int64;
                        return true;
                    }
                }
                break;
            case 'O':
                if (value.Length == 6 && value.EndsWith("bject", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.Object;
                    return true;
                }
                break;
            case 'S':
                if (value.Length == 4 && value.EndsWith("Byte", StringComparison.OrdinalIgnoreCase))
                {
                    typeCode = TypeCode.SByte;
                    return true;
                }
                if (value.Length == 6)
                {
                    if (value.EndsWith("ingle", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.Single;
                        return true;
                    }
                    if (value.EndsWith("tring", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.String;
                        return true;
                    }
                }
                break;
            case 'U':
                if (value.Length == 6)
                {
                    if (value.EndsWith("Int16", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.UInt16;
                        return true;
                    }
                    if (value.EndsWith("Int32", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.UInt32;
                        return true;
                    }
                    if (value.EndsWith("Int64", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCode = TypeCode.UInt64;
                        return true;
                    }
                }
                break;
            default:
                break;
        }

        Fail:
        Unsafe.SkipInit(out typeCode);
        return false;
    }
}
