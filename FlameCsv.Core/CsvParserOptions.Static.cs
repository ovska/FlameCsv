using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;
using static FlameCsv.CsvParserOptionDefaults;

namespace FlameCsv;

public readonly partial record struct CsvParserOptions<T>
{
    /// <summary>
    /// Returns an options instance with comma delimiter, doublequote string delimiter, backslash escape, and
    /// <see cref="System.Environment.NewLine"/> newline with no whitespace defined.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    public static CsvParserOptions<T> Environment => FromGeneric(_charEnv, _byteEnv);

    /// <summary>
    /// Returns an options instance with comma delimiter, doublequote string delimiter, backslash escape, and
    /// CRLF newline with no whitespace defined.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    public static CsvParserOptions<T> Windows => FromGeneric(_charWin, _byteWin);

    /// <summary>
    /// Returns an options instance with comma delimiter, doublequote string delimiter, backslash escape, and
    /// LF newline with no whitespace defined.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    public static CsvParserOptions<T> Unix => FromGeneric(_charUnix, _byteUnix);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CsvParserOptions<T> FromGeneric(CsvParserOptions<char> forChars, CsvParserOptions<byte> forBytes)
    {
        if (typeof(T) == typeof(char))
            return Unsafe.As<CsvParserOptions<char>, CsvParserOptions<T>>(ref forChars);

        if (typeof(T) == typeof(byte))
            return Unsafe.As<CsvParserOptions<byte>, CsvParserOptions<T>>(ref forBytes);

        return ThrowHelper.ThrowNotSupportedException<CsvParserOptions<T>>(
            $"Default options for type {typeof(T)} are not supported.");
    }
}

internal static class CsvParserOptionDefaults
{
    internal static readonly CsvParserOptions<char> _charEnv
        = new()
        {
            Delimiter = ',',
            NewLine = Environment.NewLine.AsMemory(),
            StringDelimiter = '"',
            Whitespace = ReadOnlyMemory<char>.Empty,
        };

    internal static readonly CsvParserOptions<byte> _byteEnv = _charEnv.ToUtf8Bytes();
    internal static readonly CsvParserOptions<char> _charWin = _charEnv with { NewLine = "\r\n".AsMemory() };
    internal static readonly CsvParserOptions<byte> _byteWin = _charWin.ToUtf8Bytes();
    internal static readonly CsvParserOptions<char> _charUnix = _charEnv with { NewLine = "\n".AsMemory() };
    internal static readonly CsvParserOptions<byte> _byteUnix = _charUnix.ToUtf8Bytes();
}
