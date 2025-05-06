using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Converters.Enums;

internal static class EnumExtensions
{
    public static bool CanParseNumber<T, TEnum>(ReadOnlySpan<T> source)
        where T : unmanaged, IBinaryInteger<T>
        where TEnum : struct, Enum
    {
        return
            (uint.CreateTruncating(source[0]) - '0') <= ('9' - '0') ||
            (
                (
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte) ||
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(short) ||
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(int) ||
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(long)
                ) &&
                source[0] == T.CreateTruncating('-') // JITed away for unsigned enums
            );
    }

    // GetEnumUnderlyingType is intrinsic, so this method will be optimized into a single TryParse
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseNumber<TEnum>(ReadOnlySpan<byte> source, out TEnum value) where TEnum : struct, Enum
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

        Unsafe.SkipInit(out value);
        return false;
    }

    // GetEnumUnderlyingType is intrinsic, so this method will be optimized into a single TryParse
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseNumber<TEnum>(ReadOnlySpan<char> source, out TEnum value) where TEnum : struct, Enum
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

        Unsafe.SkipInit(out value);
        return false;
    }

    // GetEnumUnderlyingType is intrinsic, so this method will be optimized into a single AND
    internal static void AddFlag<TEnum>(ref this TEnum value, TEnum flag) where TEnum : struct, Enum
    {
        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(byte))
        {
            Unsafe.As<TEnum, byte>(ref value) |= Unsafe.As<TEnum, byte>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte))
        {
            Unsafe.As<TEnum, sbyte>(ref value) |= Unsafe.As<TEnum, sbyte>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(short))
        {
            Unsafe.As<TEnum, short>(ref value) |= Unsafe.As<TEnum, short>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ushort))
        {
            Unsafe.As<TEnum, ushort>(ref value) |= Unsafe.As<TEnum, ushort>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(int))
        {
            Unsafe.As<TEnum, int>(ref value) |= Unsafe.As<TEnum, int>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(uint))
        {
            Unsafe.As<TEnum, uint>(ref value) |= Unsafe.As<TEnum, uint>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(long))
        {
            Unsafe.As<TEnum, long>(ref value) |= Unsafe.As<TEnum, long>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ulong))
        {
            Unsafe.As<TEnum, ulong>(ref value) |= Unsafe.As<TEnum, ulong>(ref flag);
        }
        else
        {
            throw new UnreachableException(typeof(TEnum).GetEnumUnderlyingType().ToString());
        }
    }

    internal static void ClearFlag<TEnum>(ref this TEnum value, TEnum flag) where TEnum : struct, Enum
    {
        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(byte))
        {
            Unsafe.As<TEnum, byte>(ref value) &= ((byte)~Unsafe.As<TEnum, byte>(ref flag));
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte))
        {
            Unsafe.As<TEnum, sbyte>(ref value) &= ((sbyte)~Unsafe.As<TEnum, sbyte>(ref flag));
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(short))
        {
            Unsafe.As<TEnum, short>(ref value) &= (short)(~Unsafe.As<TEnum, short>(ref flag));
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ushort))
        {
            Unsafe.As<TEnum, ushort>(ref value) &= (ushort)(~Unsafe.As<TEnum, ushort>(ref flag));
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(int))
        {
            Unsafe.As<TEnum, int>(ref value) &= ~Unsafe.As<TEnum, int>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(uint))
        {
            Unsafe.As<TEnum, uint>(ref value) &= ~Unsafe.As<TEnum, uint>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(long))
        {
            Unsafe.As<TEnum, long>(ref value) &= ~Unsafe.As<TEnum, long>(ref flag);
        }
        else if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ulong))
        {
            Unsafe.As<TEnum, ulong>(ref value) &= ~Unsafe.As<TEnum, ulong>(ref flag);
        }
        else
        {
            throw new UnreachableException(typeof(TEnum).GetEnumUnderlyingType().ToString());
        }
    }

    internal static int PopCount<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        return BitOperations.PopCount(value.ToBitmask());
    }

    internal static ulong ToBitmask<TEnum>(this TEnum value) where TEnum : struct, Enum
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
