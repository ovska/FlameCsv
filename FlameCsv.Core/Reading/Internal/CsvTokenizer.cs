using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

internal abstract class CsvTokenizer<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Reads fields from the data into <paramref name="metaBuffer"/>.
    /// </summary>
    /// <param name="metaBuffer">Buffer to parse the fields to</param>
    /// <param name="data">Data to read from</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <returns>Number of fields parsed to <paramref name="metaBuffer"/></returns>
    /// <returns>Number of fields parsed</returns>
    public abstract int Tokenize(
        Span<Meta> metaBuffer,
        ReadOnlySpan<T> data,
        int startIndex);

    /// <summary>
    /// Reads fields from the data into <paramref name="metaBuffer"/> until the end of the data is reached.
    /// </summary>
    /// <param name="metaBuffer">Buffer to parse the fields to</param>
    /// <param name="data">Data to read from</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <returns>Number of fields parsed to <paramref name="metaBuffer"/></returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the implementation does not support reading to the end of the data.
    /// </exception>
    public virtual int TokenizeToEnd(
        Span<Meta> metaBuffer,
        ReadOnlySpan<T> data,
        int startIndex)
    {
        throw new NotSupportedException();
    }

    public static CsvTokenizer<T>? CreateSimd(ref readonly CsvDialect<T> dialect)
    {
        if (dialect.Escape.HasValue)
        {
            return null;
        }

        object? result = null;

        if (typeof(T) == typeof(byte))
        {
            ref var dialectByte = ref Unsafe.As<CsvDialect<T>, CsvDialect<byte>>(ref Unsafe.AsRef(in dialect));
            result = ForByte.CreateSimd(in dialectByte);
        }

        if (typeof(T) == typeof(char))
        {
            ref var dialectChar = ref Unsafe.As<CsvDialect<T>, CsvDialect<char>>(ref Unsafe.AsRef(in dialect));
            result = ForChar.CreateSimd(in dialectChar);
        }

        return result as CsvTokenizer<T>;
    }

    public static CsvTokenizer<T> Create(ref readonly CsvDialect<T> dialect)
    {
        if (dialect.Escape.HasValue)
        {
            return new UnixTokenizer<T>(in dialect);
        }

        var nl = dialect.Newline;

        return nl.Length switch
        {
            1 => new ScalarTokenizer<T, NewlineParserOne<T, NoOpVector<T>>>(dialect, new(nl.First)),
            2 => new ScalarTokenizer<T, NewlineParserTwo<T, NoOpVector<T>>>(dialect, new(nl.First, nl.Second)),
            _ => throw new UnreachableException($"Unsupported newline length: {nl.Length}.")
        };
    }
}

file static class ForChar
{
    public static CsvTokenizer<char>? CreateSimd(ref readonly CsvDialect<char> dialect)
    {
        if (Vec512Char.IsSupported)
        {
            if (dialect.Newline.Length == 1)
            {
                return new SimdTokenizer<char, NewlineParserOne<char, Vec512Char>, Vec512Char>(
                    dialect,
                    new(dialect.Newline.First));
            }

            if (dialect.Newline.Length == 2)
            {
                return new SimdTokenizer<char, NewlineParserTwo<char, Vec512Char>, Vec512Char>(
                    dialect,
                    new(dialect.Newline.First, dialect.Newline.Second));
            }
        }

        if (Vec256Char.IsSupported)
        {
            if (dialect.Newline.Length == 1)
            {
                return new SimdTokenizer<char, NewlineParserOne<char, Vec256Char>, Vec256Char>(
                    dialect,
                    new(dialect.Newline.First));
            }

            if (dialect.Newline.Length == 2)
            {
                return new SimdTokenizer<char, NewlineParserTwo<char, Vec256Char>, Vec256Char>(
                    dialect,
                    new(dialect.Newline.First, dialect.Newline.Second));
            }
        }

        if (Vec128Char.IsSupported)
        {
            if (dialect.Newline.Length == 1)
            {
                return new SimdTokenizer<char, NewlineParserOne<char, Vec128Char>, Vec128Char>(
                    dialect,
                    new(dialect.Newline.First));
            }

            if (dialect.Newline.Length == 2)
            {
                return new SimdTokenizer<char, NewlineParserTwo<char, Vec128Char>, Vec128Char>(
                    dialect,
                    new(dialect.Newline.First, dialect.Newline.Second));
            }
        }

        return null;
    }
}

file static class ForByte
{
    public static CsvTokenizer<byte>? CreateSimd(ref readonly CsvDialect<byte> dialect)
    {
        if (Vec512Byte.IsSupported)
        {
            if (dialect.Newline.Length == 1)
            {
                return new SimdTokenizer<byte, NewlineParserOne<byte, Vec512Byte>, Vec512Byte>(
                    dialect,
                    new(dialect.Newline.First));
            }

            if (dialect.Newline.Length == 2)
            {
                return new SimdTokenizer<byte, NewlineParserTwo<byte, Vec512Byte>, Vec512Byte>(
                    dialect,
                    new(dialect.Newline.First, dialect.Newline.Second));
            }
        }

        if (Vec256Byte.IsSupported)
        {
            if (dialect.Newline.Length == 1)
            {
                return new SimdTokenizer<byte, NewlineParserOne<byte, Vec256Byte>, Vec256Byte>(
                    dialect,
                    new(dialect.Newline.First));
            }

            if (dialect.Newline.Length == 2)
            {
                return new SimdTokenizer<byte, NewlineParserTwo<byte, Vec256Byte>, Vec256Byte>(
                    dialect,
                    new(dialect.Newline.First, dialect.Newline.Second));
            }
        }

        if (Vec128Byte.IsSupported)
        {
            if (dialect.Newline.Length == 1)
            {
                return new SimdTokenizer<byte, NewlineParserOne<byte, Vec128Byte>, Vec128Byte>(
                    dialect,
                    new(dialect.Newline.First));
            }

            if (dialect.Newline.Length == 2)
            {
                return new SimdTokenizer<byte, NewlineParserTwo<byte, Vec128Byte>, Vec128Byte>(
                    dialect,
                    new(dialect.Newline.First, dialect.Newline.Second));
            }
        }

        return null;
    }
}
