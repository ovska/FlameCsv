using System.Diagnostics.CodeAnalysis;
using System.Runtime.Intrinsics;
using System.Text;

namespace FlameCsv.Extensions;

[ExcludeFromCodeCoverage]
internal static class VectorExtensions
{
    public static string ToAsciiString(this Vector<byte> vector)
    {
        Span<byte> bytes = stackalloc byte[Vector<byte>.Count];
        vector.CopyTo(bytes);
        return Encoding.ASCII.GetString(bytes);
    }

    public static string ToAsciiString(this Vector128<byte> vector)
    {
        Span<byte> bytes = stackalloc byte[Vector128<byte>.Count];
        vector.CopyTo(bytes);
        return Encoding.ASCII.GetString(bytes);
    }

    public static string ToAsciiString(this Vector256<byte> vector)
    {
        Span<byte> bytes = stackalloc byte[Vector256<byte>.Count];
        vector.CopyTo(bytes);
        return Encoding.ASCII.GetString(bytes);
    }

    public static string ToAsciiString(this Vector512<byte> vector)
    {
        Span<byte> bytes = stackalloc byte[Vector512<byte>.Count];
        vector.CopyTo(bytes);
        return Encoding.ASCII.GetString(bytes);
    }
}
