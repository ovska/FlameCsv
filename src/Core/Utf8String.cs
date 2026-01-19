using System.Runtime.CompilerServices;
using System.Text;

namespace FlameCsv;

/// <summary>
/// UTF-8 string containing the string and byte representations.
/// </summary>
internal sealed class Utf8String
{
    public static Utf8String Empty { get; } = new("");

    public string String { get; }
    public ReadOnlyMemory<byte> Bytes => _bytes;

    private readonly byte[] _bytes;

    public Utf8String(string? value)
    {
        String = value ?? "";
        _bytes = String.Length == 0 ? [] : Encoding.UTF8.GetBytes(String);
    }

    public static implicit operator Utf8String(string? value) => new(value);

    public static implicit operator string?(Utf8String? value) => value?.String;

    public ReadOnlyMemory<T> AsMemory<T>()
        where T : unmanaged
    {
        if (String.Length == 0)
        {
            return default;
        }

        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<T[]>(_bytes);
        }

        if (typeof(T) == typeof(char))
        {
            ReadOnlyMemory<char> chars = String.AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref chars);
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsSpan<T>()
        where T : unmanaged
    {
        if (String.Length == 0)
        {
            return default;
        }

        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<T[]>(_bytes);
        }

        if (typeof(T) == typeof(char))
        {
            return Unsafe.BitCast<ReadOnlySpan<char>, ReadOnlySpan<T>>(String);
        }

        throw Token<T>.NotSupported;
    }
}
