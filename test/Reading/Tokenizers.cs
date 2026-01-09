using System.Runtime.Intrinsics;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

internal static class Tokenizers
{
    static Tokenizers()
    {
#if !FULL_TEST_SUITE
        All = [Tokenizer.Platform, Tokenizer.Scalar];
#else
        List<Tokenizer> supported = [];
        supported.Add(Tokenizer.Scalar);
        if (Vector128.IsHardwareAccelerated)
            supported.Add(Tokenizer.Simd);
        if (Avx2Tokenizer.IsSupported)
            supported.Add(Tokenizer.Avx2);
#if NET10_0_OR_GREATER
        if (Avx512Tokenizer.IsSupported)
            supported.Add(Tokenizer.Avx512);
#endif
        if (ArmTokenizer.IsSupported)
            supported.Add(Tokenizer.Arm);
        All = [.. supported];
#endif
    }

    public static Tokenizer[] All { get; }

    internal static CsvTokenizer<T> GetTokenizer<T>(Tokenizer tokenizer, CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        return GetTokenizer(
            tokenizer switch
            {
                Tokenizer.Simd => typeof(SimdTokenizer<,,>),
                Tokenizer.Avx2 => typeof(Avx2Tokenizer<,,>),
#if NET10_0_OR_GREATER
                Tokenizer.Avx512 => typeof(Avx512Tokenizer<,,>),
#endif
                Tokenizer.Arm => typeof(ArmTokenizer<,,>),
                _ => throw new ArgumentOutOfRangeException(nameof(tokenizer), "Unknown tokenizer type: " + tokenizer),
            },
            options
        );
    }

    public static bool IsSupported(Tokenizer tokenizer) =>
        tokenizer switch
        {
            Tokenizer.Simd or Tokenizer.Platform => Vector128.IsHardwareAccelerated,
            Tokenizer.Avx2 => Avx2Tokenizer.IsSupported,
#if NET10_0_OR_GREATER
            Tokenizer.Avx512 => Avx512Tokenizer.IsSupported,
#endif
            Tokenizer.Arm => ArmTokenizer.IsSupported,
            Tokenizer.Scalar => true,
            _ => throw new ArgumentOutOfRangeException(nameof(tokenizer), "Unknown tokenizer type: " + tokenizer),
        };

    internal static CsvTokenizer<T> GetTokenizer<T>(Type type, CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        options ??= CsvOptions<T>.Default;
        return (CsvTokenizer<T>)
            type.MakeGenericType(
                    typeof(T),
                    options.Newline.IsCRLF() ? typeof(TrueConstant) : typeof(FalseConstant),
                    options.Quote.HasValue ? typeof(TrueConstant) : typeof(FalseConstant)
                )
                .GetConstructors()[0]
                .Invoke(new object[] { options })!;
    }

    public sealed class Types : TheoryData<Type>
    {
        public Types()
        {
            if (Vector128.IsHardwareAccelerated)
                Add(typeof(SimdTokenizer<,,>));

            if (Avx2Tokenizer.IsSupported)
                Add(typeof(Avx2Tokenizer<,,>));

#if NET10_0_OR_GREATER
            if (Avx512Tokenizer.IsSupported)
                Add(typeof(Avx512Tokenizer<,,>));
#endif

            if (ArmTokenizer.IsSupported)
                Add(typeof(ArmTokenizer<,,>));
        }
    }
}
