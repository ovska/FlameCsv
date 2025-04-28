using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
#if DEBUG
using Unsafe = FlameCsv.Extensions.DebugUnsafe;
#else
using Unsafe = System.Runtime.CompilerServices.Unsafe;
#endif

namespace FlameCsv;

public partial class CsvOptions<T>
{
    /// <summary>
    /// Returns a <see langword="string"/> representation of the value.
    /// </summary>
    /// <remarks>
    /// Used interchangeably with <see cref="TryGetChars"/>, and should return the exact same
    /// value for the same input.
    /// </remarks>
    public string GetAsString(ReadOnlySpan<T> value)
    {
        if (typeof(T) == typeof(char))
        {
            return value.Cast<T, char>().ToString();
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.GetString(value.Cast<T, byte>());
        }

        throw InvalidTokenTypeEx();
    }

    /// <summary>
    /// Writes <paramref name="value"/> as chars to <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// Used interchangeably with <see cref="GetAsString(ReadOnlySpan{T})"/>, and should return the exact same
    /// value for the same input.
    /// </remarks>
    /// <param name="value">Value to write</param>
    /// <param name="destination">Buffer to write the value as chars to</param>
    /// <param name="charsWritten">If successful, how many chars were written to the destination</param>
    /// <returns>True if the destination buffer was large enough and the value was written</returns>
    public bool TryGetChars(ReadOnlySpan<T> value, Span<char> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return value.Cast<T, char>().TryCopyTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetChars(value.Cast<T, byte>(), destination, out charsWritten);
        }

        throw InvalidTokenTypeEx();
    }

    /// <summary>
    /// Returns the <typeparamref name="T"/> representation of the string.
    /// </summary>
    /// <seealso cref="TryWriteChars"/>
    public ReadOnlyMemory<T> GetFromString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ReadOnlyMemory<T>.Empty;
        }

        if (typeof(T) == typeof(char))
        {
            var mem = value.AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref mem);
        }

        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<T[]>(Encoding.UTF8.GetBytes(value));
        }

        throw InvalidTokenTypeEx();
    }

    /// <summary>
    /// Writes <paramref name="value"/> as <typeparamref name="T"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="destination">Buffer to write the chars to</param>
    /// <param name="charsWritten">If successful, how many chars were written to the destination</param>
    /// <returns>True if the destination buffer was large enough and the value was written</returns>
    /// <seealso cref="GetFromString"/>
    public bool TryWriteChars(ReadOnlySpan<char> value, Span<T> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return value.Cast<char, T>().TryCopyTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetBytes(value, destination.Cast<T, byte>(), out charsWritten);
        }

        throw InvalidTokenTypeEx();
    }

    private static NotSupportedException InvalidTokenTypeEx([CallerMemberName] string? memberName = null)
    {
        return new NotSupportedException(
            $"{typeof(CsvOptions<T>).Name}.{memberName} is not supported by default for token {typeof(T)}, inherit the class and override the member.");
    }
}
