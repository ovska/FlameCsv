using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace FlameCsv.Extensions;

[ExcludeFromCodeCoverage]
internal static class VectorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint MoveMask<T>(this Vector<T> vector)
    {
        return (Vector<byte>.Count * 8) switch
        {
            128 => vector.AsVector128().ExtractMostSignificantBits(),
            256 => vector.AsVector256().ExtractMostSignificantBits(),
            512 when nuint.Size is 8 => (nuint)vector.AsVector512().ExtractMostSignificantBits(),
            var s => throw new PlatformNotSupportedException(
                $"Unsupported vector length {s} on arch {RuntimeInformation.ProcessArchitecture}."
            ),
        };
    }

    public static byte[] ToArray(this Vector<byte> vector)
    {
        byte[] bytes = new byte[Vector<byte>.Count];
        vector.CopyTo(bytes);
        return bytes;
    }

    public static byte[] ToArray(this Vector128<byte> vector)
    {
        byte[] bytes = new byte[Vector128<byte>.Count];
        vector.CopyTo(bytes);
        return bytes;
    }

    public static byte[] ToArray(this Vector256<byte> vector)
    {
        byte[] bytes = new byte[Vector256<byte>.Count];
        vector.CopyTo(bytes);
        return bytes;
    }

    public static byte[] ToArray(this Vector512<byte> vector)
    {
        byte[] bytes = new byte[Vector512<byte>.Count];
        vector.CopyTo(bytes);
        return bytes;
    }

    public static string ToAsciiString(this Vector<byte> vector) => Encoding.ASCII.GetString(vector.ToArray());

    public static string ToAsciiString(this Vector128<byte> vector) => Encoding.ASCII.GetString(vector.ToArray());

    public static string ToAsciiString(this Vector256<byte> vector) => Encoding.ASCII.GetString(vector.ToArray());

    public static string ToAsciiString(this Vector512<byte> vector) => Encoding.ASCII.GetString(vector.ToArray());
}
