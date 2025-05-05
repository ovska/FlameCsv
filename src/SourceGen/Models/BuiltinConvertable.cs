namespace FlameCsv.SourceGen.Models;

[Flags]
internal enum BuiltinConvertable
{
    None = 0,

    /// <summary>Natively supported</summary>
    Native = 1 << 0,

    /// <summary>UTF16 formattable</summary>
    Formattable = 1 << 1,

    /// <summary>UTF16 parsable</summary>
    Parsable = 1 << 2,

    /// <summary>UTF16 parsable and formattable</summary>
    Both = Formattable | Parsable,

    /// <summary>UTF8 formattable</summary>
    Utf8Formattable = 1 << 3,

    /// <summary>UTF8 parsable</summary>
    Utf8Parsable = 1 << 4,

    /// <summary>UTF8 parsable and formattable</summary>
    Utf8Both = Utf8Formattable | Utf8Parsable,

    /// <summary>Can be used for UTF8</summary>
    Utf8Any = Both | Utf8Both,
}

internal static class BuiltinConvertableExtensions
{
    public static BuiltinConvertable GetBuiltinConvertability(this ITypeSymbol type, ref readonly FlameSymbols symbols)
    {
        if (symbols.TokenType.SpecialType is not (SpecialType.System_Byte or SpecialType.System_Char))
        {
            return BuiltinConvertable.None;
        }

        bool isByte = symbols.TokenType.SpecialType == SpecialType.System_Byte;

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_String:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_DateTime:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Char:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
                return BuiltinConvertable.Native;
            case SpecialType.None:
                // check if DateTimeOffset, TimeSpan, or Guid
                if (symbols.IsDateTimeOffset(type) || symbols.IsTimeSpan(type) || symbols.IsGuid(type))
                {
                    return BuiltinConvertable.Native;
                }

                break;
            default:
                return BuiltinConvertable.None;
        }

        BuiltinConvertable result = BuiltinConvertable.None;

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.IsGenericType && iface.TypeArguments.Length == 1)
            {
                var unbound = iface.ConstructUnboundGenericType();

                if (
                    isByte &&
                    symbols.IsIUtf8SpanParsable(unbound) &&
                    SymbolEqualityComparer.Default.Equals(type, iface.TypeArguments[0]))
                {
                    result |= BuiltinConvertable.Utf8Parsable;
                }
                else if (
                    symbols.IsISpanParsable(unbound) &&
                    SymbolEqualityComparer.Default.Equals(type, iface.TypeArguments[0]))
                {
                    result |= BuiltinConvertable.Parsable;
                }
            }
            else
            {
                if (isByte && symbols.IsIUtf8SpanFormattable(iface))
                {
                    result |= BuiltinConvertable.Utf8Formattable;
                }
                else if (symbols.IsISpanFormattable(iface))
                {
                    result |= BuiltinConvertable.Formattable;
                }
            }
        }

        return result;
    }
}
