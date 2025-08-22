using System.Diagnostics.CodeAnalysis;
using System.Runtime.Intrinsics;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal abstract class CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Reads fields from the data into <paramref name="buffer"/> until the end of the data is reached.
    /// </summary>
    /// <param name="buffer">Buffer to parse the records to</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="data">Data to read from</param>
    /// <param name="readToEnd">Whether to read to end even if data has no trailing newline</param>
    /// <returns>Number of fields read</returns>
    public abstract int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data, bool readToEnd);
}

internal static class CsvTokenizer
{
    [ExcludeFromCodeCoverage]
    public static CsvPartialTokenizer<T>? CreateSimd<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (options.Escape is not null || !Vector128.IsHardwareAccelerated)
        {
            return null;
        }

#if NET10_0_OR_GREATER
        if (Avx512Tokenizer.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new Avx512Tokenizer<T, NewlineCRLF>(options)
                : new Avx512Tokenizer<T, NewlineLF>(options);
        }
#endif

        if (Avx2Tokenizer.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new Avx2Tokenizer<T, NewlineCRLF>(options)
                : new Avx2Tokenizer<T, NewlineLF>(options);
        }

        return options.Newline.IsCRLF()
            ? new SimdTokenizer<T, NewlineCRLF>(options)
            : new SimdTokenizer<T, NewlineLF>(options);
    }

    public static CsvTokenizer<T> Create<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (options.Escape.HasValue)
        {
            return new UnixTokenizer<T>(options);
        }

        return options.Newline.IsCRLF()
            ? new ScalarTokenizer<T, NewlineCRLF>(options)
            : new ScalarTokenizer<T, NewlineLF>(options);
    }
}
