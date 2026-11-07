using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

/// <summary>
/// Verifies that the platform matrix workflow tests the configuration it claims to.
/// Runtime ISA settings and runner hardware vary, so without this check a job could silently
/// fall back to another tokenizer (e.g. an "AVX-512" job on a CPU without AVX-512 testing AVX2).
/// </summary>
public static class PlatformMatrixTests
{
    /// <summary>
    /// Set by <c>.github/workflows/platform-matrix.yml</c>.
    /// </summary>
    private const string ExpectedPlatformVariable = "FLAMECSV_EXPECTED_PLATFORM";

    [Fact]
    public static void Should_Run_On_Expected_Platform()
    {
        string? expected = Environment.GetEnvironmentVariable(ExpectedPlatformVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(expected), $"{ExpectedPlatformVariable} is not set");

        Type? byteTokenizer = GetTokenizerType<byte>();
        Type? charTokenizer = GetTokenizerType<char>();
        string actual = Describe(byteTokenizer);

        TestContext.Current.TestOutputHelper?.WriteLine($"Expected platform '{expected}', actual: {actual}");

        // char and byte must always pick the same implementation
        Assert.Equal(byteTokenizer, charTokenizer);

        switch (expected)
        {
            case "scalar":
                Require(byteTokenizer is null, "no SIMD tokenizer", actual);
                Require(!Vector128.IsHardwareAccelerated, "no hardware SIMD", actual);
                break;
            case "simd128":
                Require(byteTokenizer == typeof(SimdTokenizer<,,>), "the generic SIMD tokenizer", actual);
                Require(!Vector256.IsHardwareAccelerated, "128-bit vectors only", actual);
                break;
            case "avx2":
                Require(byteTokenizer == typeof(Avx2Tokenizer<,,>), "the AVX2 tokenizer", actual);
                Require(!Avx512BW.IsSupported, "AVX-512 disabled", actual);
                break;
            case "avx2-avx512bw":
                Require(
                    Avx512BW.IsSupported,
                    "a CPU with AVX-512 (runner hardware varies, re-run the job if it lacks AVX-512)",
                    actual
                );
                Require(byteTokenizer == typeof(Avx2Tokenizer<,,>), "the AVX2 tokenizer", actual);
                break;
            case "avx512":
                Require(
                    Avx512Vbmi2.IsSupported,
                    "a CPU with AVX-512 VBMI2 (runner hardware varies, re-run the job if it lacks AVX-512)",
                    actual
                );
                Require(byteTokenizer == typeof(Avx512Tokenizer<,,>), "the AVX-512 tokenizer", actual);
                break;
            case "arm64":
                Require(RuntimeInformation.ProcessArchitecture == Architecture.Arm64, "an ARM64 process", actual);
                Require(byteTokenizer == typeof(ArmTokenizer<,,>), "the ARM tokenizer", actual);
                break;
            case "x86":
                Require(RuntimeInformation.ProcessArchitecture == Architecture.X86, "a 32-bit x86 process", actual);
                Require(byteTokenizer is not null, "a SIMD tokenizer", actual);
                break;
            default:
                Assert.Fail($"Unknown {ExpectedPlatformVariable} value '{expected}'.");
                break;
        }
    }

    private static Type? GetTokenizerType<T>()
        where T : unmanaged, IBinaryInteger<T>
    {
        // a fresh instance, as the tokenizers are cached per options instance
        var (_, simd) = new CsvOptions<T>().GetTokenizers();
        return simd?.GetType().GetGenericTypeDefinition();
    }

    private static void Require(bool condition, string requirement, string actual)
    {
        Assert.True(condition, $"Expected {requirement}, but the process has: {actual}");
    }

    private static string Describe(Type? tokenizer)
    {
        string name = tokenizer is null ? "scalar only" : tokenizer.Name[..tokenizer.Name.IndexOf('`')];

        return $"tokenizer={name}, process={RuntimeInformation.ProcessArchitecture}, "
            + $"Vector128={Vector128.IsHardwareAccelerated}, Vector256={Vector256.IsHardwareAccelerated}, "
            + $"Vector512={Vector512.IsHardwareAccelerated}, Vector<T>={Vector<byte>.Count * 8} bits, "
            + $"AVX2={Avx2.IsSupported}, AVX512BW={Avx512BW.IsSupported}, AVX512VBMI2={Avx512Vbmi2.IsSupported}";
    }
}
