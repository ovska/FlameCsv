﻿using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    /// <summary>
    /// Returns a <see langword="string"/> representation of the value.
    /// See also <see cref="TryGetChars(ReadOnlySpan{T}, Span{char}, out int)"/>
    /// </summary>
    public virtual string GetAsString(ReadOnlySpan<T> value)
    {
        if (typeof(T) == typeof(char))
        {
            return value.UnsafeCast<T, char>().ToString();
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.GetString(value.UnsafeCast<T, byte>());
        }

        ThrowInvalidTokenType(nameof(GetAsString));
        return default!;
    }

    /// <summary>
    /// Returns the <typeparamref name="T"/> representation of the string.
    /// </summary>
    /// <seealso cref="TryWriteChars"/>
    public virtual ReadOnlyMemory<T> GetFromString(string? value)
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

        ThrowInvalidTokenType(nameof(GetFromString));
        return default;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as chars to <paramref name="destination"/>.
    /// See also <see cref="GetAsString(ReadOnlySpan{T})"/>.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="destination">Buffer to write the value as chars to</param>
    /// <param name="charsWritten">If successful, how many chars were written to the destination</param>
    /// <returns>True if the destination buffer was large enough and the value was written</returns>
    public virtual bool TryGetChars(ReadOnlySpan<T> value, Span<char> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return value.UnsafeCast<T, char>().TryWriteTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetChars(value.UnsafeCast<T, byte>(), destination, out charsWritten);
        }

        ThrowInvalidTokenType(nameof(TryGetChars));
        Unsafe.SkipInit(out charsWritten);
        return default;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as <typeparamref name="T"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="destination">Buffer to write the chars to</param>
    /// <param name="charsWritten">If successful, how many chars were written to the destination</param>
    /// <returns>True if the destination buffer was large enough and the value was written</returns>
    public virtual bool TryWriteChars(ReadOnlySpan<char> value, Span<T> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return value.UnsafeCast<char, T>().TryWriteTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetBytes(value, destination.UnsafeCast<T, byte>(), out charsWritten);
        }

        ThrowInvalidTokenType(nameof(TryWriteChars));
        Unsafe.SkipInit(out charsWritten);
        return default;
    }
}
