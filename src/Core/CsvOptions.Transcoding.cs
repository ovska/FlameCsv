using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    /// <summary>
    /// Returns a <see langword="string"/> representation of the value.
    /// </summary>
    /// <remarks>
    /// Can be used interchangeably with <see cref="TryGetChars"/>.
    /// </remarks>
    public static string GetAsString(scoped ReadOnlySpan<T> value)
    {
        return typeof(T) == typeof(byte) ? Encoding.UTF8.GetString(MemoryMarshal.AsBytes(value)) : value.ToString();
    }

    /// <summary>
    /// Writes <paramref name="value"/> as chars to <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// Can be used interchangeably with <see cref="GetAsString"/>.
    /// </remarks>
    /// <param name="value">Value to write</param>
    /// <param name="destination">Buffer to write the value as chars to</param>
    /// <param name="charsWritten">If successful, how many chars were written to the destination</param>
    /// <returns>True if the destination buffer was large enough and the value was written</returns>
    public static bool TryGetChars(scoped ReadOnlySpan<T> value, scoped Span<char> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return value.Cast<T, char>().TryCopyTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetChars(MemoryMarshal.AsBytes(value), destination, out charsWritten);
        }

        throw Token<T>.NotSupported;
    }

    /// <summary>
    /// Returns the <typeparamref name="T"/> representation of the string.
    /// </summary>
    /// <seealso cref="TryWriteChars"/>
    public static ReadOnlyMemory<T> GetFromString(string? value)
    {
        if (typeof(T) == typeof(char))
        {
            var mem = value.AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref mem);
        }

        if (typeof(T) == typeof(byte))
        {
            return string.IsNullOrEmpty(value) ? default : Unsafe.As<T[]>(Encoding.UTF8.GetBytes(value));
        }

        throw Token<T>.NotSupported;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as <typeparamref name="T"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="destination">Buffer to write the chars to</param>
    /// <param name="charsWritten">If successful, how many chars were written to the destination</param>
    /// <returns>True if the destination buffer was large enough and the value was written</returns>
    /// <seealso cref="GetFromString"/>
    public static bool TryWriteChars(scoped ReadOnlySpan<char> value, scoped Span<T> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return value.Cast<char, T>().TryCopyTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetBytes(value, MemoryMarshal.AsBytes(destination), out charsWritten);
        }

        throw Token<T>.NotSupported;
    }
}
