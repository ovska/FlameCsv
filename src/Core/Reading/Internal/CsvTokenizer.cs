using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal static class CsvTokenizer
{
    [ExcludeFromCodeCoverage]
    public static CsvTokenizer<T>? Create<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
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

    public static CsvScalarTokenizer<T> CreateScalar<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        return options.Newline.IsCRLF()
            ? new ScalarTokenizer<T, NewlineCRLF>(options)
            : new ScalarTokenizer<T, NewlineLF>(options);
    }
}
