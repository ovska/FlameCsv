using System.Runtime.CompilerServices;
using System.Text;

namespace FlameCsv;

/// <summary>
/// UTF-8 string containing the string and byte representations.
/// </summary>
internal sealed class Utf8String
{
    public string String { get; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte[] InitBytes() => _bytes ??= Encoding.UTF8.GetBytes(String);

    private byte[]? _bytes;

    public Utf8String(string? value)
    {
        String = value ?? "";
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
            return Unsafe.As<T[]>(_bytes ?? InitBytes());
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
            return Unsafe.As<T[]>(_bytes ?? InitBytes());
        }

        if (typeof(T) == typeof(char))
        {
            ReadOnlySpan<char> chars = String.AsSpan();
            return Unsafe.As<ReadOnlySpan<char>, ReadOnlySpan<T>>(ref chars);
        }

        throw Token<T>.NotSupported;
    }
}
