using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;
using static FlameCsv.CsvTokenDefaults;

namespace FlameCsv;

public readonly partial record struct CsvTokens<T>
{
    /// <summary>
    /// Returns an options instance with comma delimiter, doublequote string delimiter, backslash escape, and
    /// <see cref="System.Environment.NewLine"/> newline with no whitespace defined.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    public static CsvTokens<T> Environment => FromGeneric(ref _charEnv, ref _byteEnv);

    /// <summary>
    /// Returns an options instance with comma delimiter, doublequote string delimiter, backslash escape, and
    /// CRLF newline with no whitespace defined.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    public static CsvTokens<T> Windows => FromGeneric(ref _charWin, ref _byteWin);

    /// <summary>
    /// Returns an options instance with comma delimiter, doublequote string delimiter, backslash escape, and
    /// LF newline with no whitespace defined.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    public static CsvTokens<T> Unix => FromGeneric(ref _charUnix, ref _byteUnix);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CsvTokens<T> FromGeneric(ref CsvTokens<char> forChars, ref CsvTokens<byte> forBytes)
    {
        if (typeof(T) == typeof(char))
            return Unsafe.As<CsvTokens<char>, CsvTokens<T>>(ref forChars);

        if (typeof(T) == typeof(byte))
            return Unsafe.As<CsvTokens<byte>, CsvTokens<T>>(ref forBytes);

        return ThrowHelper.ThrowNotSupportedException<CsvTokens<T>>(
            $"Default options for type {typeof(T)} are not supported.");
    }

    /// <summary>
    /// Returns default options with CRLF newline for char and byte, uninitialized otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CsvTokens<T> GetDefaultForOptions()
    {
        if (typeof(T) == typeof(char))
            return Unsafe.As<CsvTokens<char>, CsvTokens<T>>(ref _charWin);

        if (typeof(T) == typeof(byte))
            return Unsafe.As<CsvTokens<byte>, CsvTokens<T>>(ref _byteWin);

        return default;
    }
}

// separate class to avoid statics in generic type
internal static class CsvTokenDefaults
{
    internal static CsvTokens<char> _charEnv
        = new()
        {
            Delimiter = ',',
            NewLine = Environment.NewLine.AsMemory(),
            StringDelimiter = '"',
            Whitespace = ReadOnlyMemory<char>.Empty,
        };

    internal static CsvTokens<byte> _byteEnv = _charEnv.ToUtf8Bytes();
    internal static CsvTokens<char> _charWin = _charEnv with { NewLine = "\r\n".AsMemory() };
    internal static CsvTokens<byte> _byteWin = _charWin.ToUtf8Bytes();
    internal static CsvTokens<char> _charUnix = _charEnv with { NewLine = "\n".AsMemory() };
    internal static CsvTokens<byte> _byteUnix = _charUnix.ToUtf8Bytes();
}
