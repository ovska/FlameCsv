using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal abstract class CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Number of characters deemed
    /// </summary>
    public abstract int PreferredLength { get; }

    /// <summary>
    /// Reads fields from the data into <paramref name="metaBuffer"/>.
    /// </summary>
    /// <param name="metaBuffer">Buffer to parse the fields to</param>
    /// <param name="data">Data to read from</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <returns>Number of fields parsed to <paramref name="metaBuffer"/></returns>
    public abstract int Tokenize(Span<Meta> metaBuffer, ReadOnlySpan<T> data, int startIndex);
}

internal abstract class CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Reads fields from the data into <paramref name="metaBuffer"/> until the end of the data is reached.
    /// </summary>
    /// <param name="metaBuffer">Buffer to parse the fields to</param>
    /// <param name="data">Data to read from</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="readToEnd">Whether to read to end even if data has no trailing newline</param>
    /// <returns>Number of fields parsed to <paramref name="metaBuffer"/></returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the implementation does not support reading to the end of the data.
    /// </exception>
    public virtual int Tokenize(Span<Meta> metaBuffer, ReadOnlySpan<T> data, int startIndex, bool readToEnd)
    {
        throw new NotSupportedException();
    }
}

internal static class CsvTokenizer
{
    public static CsvPartialTokenizer<T>? CreateSimd<T>(ref readonly CsvDialect<T> dialect)
        where T : unmanaged, IBinaryInteger<T>
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

        return result as CsvPartialTokenizer<T>;
    }

    public static CsvTokenizer<T> Create<T>(ref readonly CsvDialect<T> dialect)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (dialect.Escape.HasValue)
        {
            return new UnixTokenizer<T>(in dialect);
        }

        return dialect.Newline.IsCRLF()
            ? new ScalarTokenizer<T, NewlineParserTwo<T, NoOpVector<T>>>(dialect)
            : new ScalarTokenizer<T, NewlineParserOne<T, NoOpVector<T>>>(dialect);
    }
}

file static class ForChar
{
    public static CsvPartialTokenizer<char>? CreateSimd(ref readonly CsvDialect<char> dialect)
    {
        // TODO: benchmark 256 vs 512 on non-x86 platforms; RuntimeInformation.ProcessArchitecture is X86 or X64.. etc
        // on x86 with AVX512, 256 is faster in all cases except 3% slower on dense non-quoted data
        // and up to 6% slower on dense or quoted data

        // use "else" instead of just "if" to reduce JITted code size if multiple SIMD types are supported
        if (Vec256Char.IsSupported)
        {
            return dialect.Newline.IsCRLF()
                ? new SimdTokenizer<char, NewlineParserTwo<char, Vec256Char>, Vec256Char>(dialect)
                : new SimdTokenizer<char, NewlineParserOne<char, Vec256Char>, Vec256Char>(dialect);
        }
        else if (Vec512Char.IsSupported)
        {
            return dialect.Newline.IsCRLF()
                ? new SimdTokenizer<char, NewlineParserTwo<char, Vec512Char>, Vec512Char>(dialect)
                : new SimdTokenizer<char, NewlineParserOne<char, Vec512Char>, Vec512Char>(dialect);
        }
        else if (Vec128Char.IsSupported)
        {
            return dialect.Newline.IsCRLF()
                ? new SimdTokenizer<char, NewlineParserTwo<char, Vec128Char>, Vec128Char>(dialect)
                : new SimdTokenizer<char, NewlineParserOne<char, Vec128Char>, Vec128Char>(dialect);
        }

        return null;
    }
}

file static class ForByte
{
    public static CsvPartialTokenizer<byte>? CreateSimd(ref readonly CsvDialect<byte> dialect)
    {
        // TODO: benchmark 256 vs 512 on non-x86 platforms

        // use if-else to reduce JITted code size if multiple SIMD types are supported
        if (Vec256Byte.IsSupported)
        {
            return dialect.Newline.IsCRLF()
                ? new SimdTokenizer<byte, NewlineParserTwo<byte, Vec256Byte>, Vec256Byte>(dialect)
                : new SimdTokenizer<byte, NewlineParserOne<byte, Vec256Byte>, Vec256Byte>(dialect);
        }
        else if (Vec512Byte.IsSupported)
        {
            return dialect.Newline.IsCRLF()
                ? new SimdTokenizer<byte, NewlineParserTwo<byte, Vec512Byte>, Vec512Byte>(dialect)
                : new SimdTokenizer<byte, NewlineParserOne<byte, Vec512Byte>, Vec512Byte>(dialect);
        }
        else if (Vec128Byte.IsSupported)
        {
            return dialect.Newline.IsCRLF()
                ? new SimdTokenizer<byte, NewlineParserTwo<byte, Vec128Byte>, Vec128Byte>(dialect)
                : new SimdTokenizer<byte, NewlineParserOne<byte, Vec128Byte>, Vec128Byte>(dialect);
        }

        return null;
    }
}

/*
| Method | Alt   | Chars | Mean       | StdDev  | Ratio |
|------- |------ |------ |-----------:|--------:|------:|
| V128   | False | False | 1,539.0 us | 2.98 us |  1.00 |
| V256   | False | False | 1,149.3 us | 9.75 us |  0.75 |
| V512   | False | False | 1,238.2 us | 1.63 us |  0.80 |
|        |       |       |            |         |       |
| V128   | False | True  | 1,674.6 us | 6.86 us |  1.00 |
| V256   | False | True  | 1,239.4 us | 7.83 us |  0.74 |
| V512   | False | True  | 1,340.8 us | 6.36 us |  0.80 |
|        |       |       |            |         |       |
| V128   | True  | False |   582.8 us | 2.02 us |  1.00 |
| V256   | True  | False |   440.5 us | 1.43 us |  0.76 |
| V512   | True  | False |   423.7 us | 3.08 us |  0.73 |
|        |       |       |            |         |       |
| V128   | True  | True  |   631.8 us | 2.17 us |  1.00 |
| V256   | True  | True  |   470.9 us | 3.43 us |  0.75 |
| V512   | True  | True  |   479.1 us | 1.02 us |  0.76 |
*/
