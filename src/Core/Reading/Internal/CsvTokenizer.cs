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
                ? new Avx512Tokenizer<T, TrueConstant>(options)
                : new Avx512Tokenizer<T, FalseConstant>(options);
        }
#endif

        if (Avx2Tokenizer.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new Avx2Tokenizer<T, TrueConstant>(options)
                : new Avx2Tokenizer<T, FalseConstant>(options);
        }

        return options.Newline.IsCRLF()
            ? new SimdTokenizer<T, TrueConstant>(options)
            : new SimdTokenizer<T, FalseConstant>(options);
    }

    public static CsvScalarTokenizer<T> CreateScalar<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        return options.Newline.IsCRLF()
            ? new ScalarTokenizer<T, TrueConstant>(options)
            : new ScalarTokenizer<T, FalseConstant>(options);
    }
}
