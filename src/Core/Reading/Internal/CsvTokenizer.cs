using FlameCsv.Extensions;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal abstract class CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Number of Tacters deemed
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
        throw new NotSupportedException("This tokenizer does not support reading to the end of the data.");
    }
}

internal static class CsvTokenizer
{
    public static CsvPartialTokenizer<T>? CreateSimd<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (options.Escape.HasValue)
        {
            return null;
        }

        // on x86 with AVX512, 256 is faster in all cases except 3% slower on dense non-quoted data
        // and up to 6% slower on dense or quoted data

        // use "else" instead of just "if" to reduce JITted code size if multiple SIMD types are supported
        if (Vec256.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new SimdTokenizer<T, NewlineCRLF<Vec256>, Vec256>(options)
                : new SimdTokenizer<T, NewlineLF<Vec256>, Vec256>(options);
        }

        if (Vec512.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new SimdTokenizer<T, NewlineCRLF<Vec512>, Vec512>(options)
                : new SimdTokenizer<T, NewlineLF<Vec512>, Vec512>(options);
        }

        if (Vec128.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new SimdTokenizer<T, NewlineCRLF<Vec128>, Vec128>(options)
                : new SimdTokenizer<T, NewlineLF<Vec128>, Vec128>(options);
        }

        return null;
    }

    public static CsvTokenizer<T> Create<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (options.Escape.HasValue)
        {
            return new UnixTokenizer<T>(options);
        }

        return options.Newline.IsCRLF()
            ? new ScalarTokenizer<T, NewlineCRLF<NoOpVector>>(options)
            : new ScalarTokenizer<T, NewlineLF<NoOpVector>>(options);
    }
}

/*
| Method | Alt   | Ts | Mean       | StdDev  | Ratio |
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
