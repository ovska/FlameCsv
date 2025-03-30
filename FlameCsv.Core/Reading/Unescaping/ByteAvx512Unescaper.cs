#if false
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Reading.Unescaping;

internal readonly struct ByteAvx512Unescaper : ISimdUnescaper<byte, ulong, Vector512<byte>>
{
    public static bool IsSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx512Vbmi.IsSupported && Vector512.IsHardwareAccelerated;
    }

    public static int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector512<byte>.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> CreateVector(byte value) => Vector512.Create(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> LoadVector(ref readonly byte value, nuint offset = 0)
    {
        return Vector512.LoadUnsafe(in value, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreVector(Vector512<byte> vector, ref byte destination, nuint offset = 0)
    {
        vector.StoreUnsafe(ref destination, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong FindQuotes(Vector512<byte> value, Vector512<byte> quote)
    {
        return Vector512.Equals(value, quote).ExtractMostSignificantBits();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compress(Vector512<byte> value, ulong mask, ref byte destination, nuint offset = 0)
    {
        // TODO: implement when CompressMaskZero or similar is available in .NET
    }
}
#endif
