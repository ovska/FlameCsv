using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Converters.Enums;

internal static class EnumExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanParseNumber<T, TEnum>(ReadOnlySpan<T> source)
        where T : unmanaged, IBinaryInteger<T>
        where TEnum : struct, Enum
    {
        return (uint.CreateTruncating(source[0]) - '0') <= ('9' - '0')
            || (
                (
                    // intrinsic
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte)
                    || typeof(TEnum).GetEnumUnderlyingType() == typeof(short)
                    || typeof(TEnum).GetEnumUnderlyingType() == typeof(int)
                    || typeof(TEnum).GetEnumUnderlyingType() == typeof(long)
                )
                && source[0] == T.CreateTruncating('-') // JITed away for unsigned enums
            );
    }

    // GetEnumUnderlyingType is intrinsic, so this method will be optimized into a single TryParse
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseNumber<TEnum>(ReadOnlySpan<byte> source, out TEnum value)
        where TEnum : struct, Enum
    {
        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(byte) && byte.TryParse(source, out byte b))
        {
            value = Unsafe.As<byte, TEnum>(ref b);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte) && sbyte.TryParse(source, out sbyte sb))
        {
            value = Unsafe.As<sbyte, TEnum>(ref sb);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(short) && short.TryParse(source, out short sh))
        {
            value = Unsafe.As<short, TEnum>(ref sh);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ushort) && ushort.TryParse(source, out ushort ush))
        {
            value = Unsafe.As<ushort, TEnum>(ref ush);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(int) && int.TryParse(source, out int i))
        {
            value = Unsafe.As<int, TEnum>(ref i);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(uint) && uint.TryParse(source, out uint ui))
        {
            value = Unsafe.As<uint, TEnum>(ref ui);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(long) && long.TryParse(source, out long l))
        {
            value = Unsafe.As<long, TEnum>(ref l);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ulong) && ulong.TryParse(source, out ulong ul))
        {
            value = Unsafe.As<ulong, TEnum>(ref ul);
            return true;
        }

        value = default;
        return false;
    }

    // GetEnumUnderlyingType is intrinsic, so this method will be optimized into a single TryParse
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseNumber<TEnum>(ReadOnlySpan<char> source, out TEnum value)
        where TEnum : struct, Enum
    {
        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(byte) && byte.TryParse(source, out byte b))
        {
            value = Unsafe.As<byte, TEnum>(ref b);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte) && sbyte.TryParse(source, out sbyte sb))
        {
            value = Unsafe.As<sbyte, TEnum>(ref sb);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(short) && short.TryParse(source, out short sh))
        {
            value = Unsafe.As<short, TEnum>(ref sh);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ushort) && ushort.TryParse(source, out ushort ush))
        {
            value = Unsafe.As<ushort, TEnum>(ref ush);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(int) && int.TryParse(source, out int i))
        {
            value = Unsafe.As<int, TEnum>(ref i);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(uint) && uint.TryParse(source, out uint ui))
        {
            value = Unsafe.As<uint, TEnum>(ref ui);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(long) && long.TryParse(source, out long l))
        {
            value = Unsafe.As<long, TEnum>(ref l);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ulong) && ulong.TryParse(source, out ulong ul))
        {
            value = Unsafe.As<ulong, TEnum>(ref ul);
            return true;
        }

        value = default;
        return false;
    }

    internal static void SetFlag<TEnum>(ref this TEnum value, TEnum flag, bool enabled)
        where TEnum : struct, Enum
    {
        if (enabled)
        {
            AddFlag(ref value, flag);
        }
        else
        {
            ClearFlag(ref value, flag);
        }
    }

    internal static bool GetFlag<TEnum>(this TEnum value, TEnum flag)
        where TEnum : struct, Enum
    {
        return Unsafe.SizeOf<TEnum>() switch
        {
            sizeof(byte) => (Unsafe.BitCast<TEnum, byte>(value) & Unsafe.BitCast<TEnum, byte>(flag)) != 0,
            sizeof(ushort) => (Unsafe.BitCast<TEnum, short>(value) & Unsafe.BitCast<TEnum, short>(flag)) != 0,
            sizeof(uint) => (Unsafe.BitCast<TEnum, uint>(value) & Unsafe.BitCast<TEnum, uint>(flag)) != 0,
            sizeof(ulong) => (Unsafe.BitCast<TEnum, ulong>(value) & Unsafe.BitCast<TEnum, ulong>(flag)) != 0,
            _ => throw new UnreachableException(typeof(TEnum).FullName),
        };
    }

    internal static void AddFlag<TEnum>(ref this TEnum value, TEnum flag)
        where TEnum : struct, Enum
    {
        // runtime constant
        switch (Unsafe.SizeOf<TEnum>())
        {
            case sizeof(byte):
                Unsafe.As<TEnum, byte>(ref value) |= Unsafe.BitCast<TEnum, byte>(flag);
                break;
            case sizeof(ushort):
                Unsafe.As<TEnum, short>(ref value) |= Unsafe.BitCast<TEnum, short>(flag);
                break;
            case sizeof(uint):
                Unsafe.As<TEnum, uint>(ref value) |= Unsafe.BitCast<TEnum, uint>(flag);
                break;
            case sizeof(ulong):
                Unsafe.As<TEnum, ulong>(ref value) |= Unsafe.BitCast<TEnum, ulong>(flag);
                break;
            default:
                throw new UnreachableException(typeof(TEnum).FullName);
        }
    }

    internal static void ClearFlag<TEnum>(ref this TEnum value, TEnum flag)
        where TEnum : struct, Enum
    {
        // runtime constant
        switch (Unsafe.SizeOf<TEnum>())
        {
            case sizeof(byte):
                Unsafe.As<TEnum, byte>(ref value) &= (byte)~Unsafe.BitCast<TEnum, byte>(flag);
                break;
            case sizeof(ushort):
                Unsafe.As<TEnum, ushort>(ref value) &= (ushort)~Unsafe.BitCast<TEnum, ushort>(flag);
                break;
            case sizeof(uint):
                Unsafe.As<TEnum, uint>(ref value) &= ~Unsafe.BitCast<TEnum, uint>(flag);
                break;
            case sizeof(ulong):
                Unsafe.As<TEnum, ulong>(ref value) &= ~Unsafe.BitCast<TEnum, ulong>(flag);
                break;
            default:
                throw new UnreachableException(typeof(TEnum).FullName);
        }
    }

    internal static ulong ToBitmask<TEnum>(this TEnum value)
        where TEnum : struct, Enum
    {
        return Unsafe.SizeOf<TEnum>() switch
        {
            sizeof(byte) => Unsafe.BitCast<TEnum, byte>(value),
            sizeof(ushort) => Unsafe.BitCast<TEnum, ushort>(value),
            sizeof(uint) => Unsafe.BitCast<TEnum, uint>(value),
            sizeof(ulong) => Unsafe.BitCast<TEnum, ulong>(value),
            _ => throw new UnreachableException(typeof(TEnum).FullName),
        };
    }
}
