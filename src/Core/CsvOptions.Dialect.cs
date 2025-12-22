using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using FlameCsv.Utilities;

namespace FlameCsv;

internal readonly struct Dialect<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public readonly CsvFieldTrimming Trimming;
    public readonly T Quote;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dialect(CsvOptions<T> options)
    {
        Quote = T.CreateTruncating(options.Quote);

        // remove undefined bits so we can cmp against zero later
        Trimming = options.Trimming & CsvFieldTrimming.Both;
    }
}

public partial class CsvOptions<T>
{
    private char _delimiter = ',';
    private char _quote = '"';
    private CsvNewline _newline = CsvNewline.CRLF;
    private CsvFieldTrimming _trimming = CsvFieldTrimming.None;

    /// <summary>
    /// The separator character between CSV fields. Default value: <c>,</c>
    /// </summary>
    public char Delimiter
    {
        get => _delimiter;
        set
        {
            DialectHelper.ValidateToken(value);
            this.SetValue(ref _delimiter, value);
        }
    }

    /// <summary>
    /// Characted used to quote strings containing control characters. Default value: <c>"</c>
    /// </summary>
    public char Quote
    {
        get => _quote;
        set
        {
            DialectHelper.ValidateToken(value);
            this.SetValue(ref _quote, value);
        }
    }

    /// <summary>
    /// 1-2 characters long newline string. The default is <c>CRLF</c>.
    /// </summary>
    /// <remarks>
    /// If the newline is 2 characters long, either of the characters is allowed as a record delimiter,
    /// e.g., the default value can read CSV with only <c>LF</c> newlines.
    /// </remarks>
    public CsvNewline Newline
    {
        get => _newline;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((byte)value, (byte)CsvNewline.Platform, nameof(value));
            this.SetValue(ref _newline, value);
        }
    }

    /// <summary>
    /// Whether to trim leading and/or trailing spaces from fields.<br/>
    /// The default value is <see cref="CsvFieldTrimming.None"/>.
    /// </summary>
    /// <seealso cref="FieldQuoting"/>
    public CsvFieldTrimming Trimming
    {
        get => _trimming;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((byte)value, (byte)CsvFieldTrimming.Both, nameof(value));
            this.SetValue(ref _trimming, value);
        }
    }

    /// <summary>
    /// Ensures <see cref="Delimiter"/> and <see cref="Quote"/> are valid.
    /// </summary>
    /// <exception cref="CsvConfigurationException"/>
    public void Validate()
    {
        Check.False(_delimiter is '\0' or '\r' or '\n' or ' ');
        Check.False(_quote is '\0' or '\r' or '\n' or ' ');

        // already validated at this point
        if (IsReadOnly)
        {
            return;
        }

        StringScratch scratch = default;
        using ValueListBuilder<string> errors = new(scratch);

        if (_delimiter.Equals(_quote))
        {
            errors.Append("Delimiter and Quote must not be equal.");
        }

        if (errors.Length != 0)
        {
            DialectHelper.ThrowException(errors.AsSpan(), _delimiter, _quote);
        }
    }

    private CsvScalarTokenizer<T>? _scalarTokenizer;
    private CsvTokenizer<T>? _simdTokenizer;
    private bool _tokenizersCreated;

    internal (CsvScalarTokenizer<T> scalar, CsvTokenizer<T>? simd) GetTokenizers()
    {
        if (!_tokenizersCreated)
        {
            MakeReadOnly();

            bool isCRLF = Newline.IsCRLF();

#if NET10_0_OR_GREATER
            if (Avx512Tokenizer.IsSupported)
            {
                _simdTokenizer = isCRLF
                    ? new Avx512Tokenizer<T, TrueConstant>(this)
                    : new Avx512Tokenizer<T, FalseConstant>(this);
            }
            else
#endif
            if (Avx2Tokenizer.IsSupported)
            {
                _simdTokenizer = isCRLF
                    ? new Avx2Tokenizer<T, TrueConstant>(this)
                    : new Avx2Tokenizer<T, FalseConstant>(this);
            }
            else if (Vector.IsHardwareAccelerated) // implies SSE, WASM or NEON
            {
                _simdTokenizer = isCRLF
                    ? new SimdTokenizer<T, TrueConstant>(this)
                    : new SimdTokenizer<T, FalseConstant>(this);
            }

            _scalarTokenizer = isCRLF
                ? new ScalarTokenizer<T, TrueConstant>(this)
                : new ScalarTokenizer<T, FalseConstant>(this);

            _tokenizersCreated = true;
        }

        Check.NotNull(_scalarTokenizer);
        return (_scalarTokenizer, _simdTokenizer);
    }

    /// <summary>
    /// Returns search values that determine if a field needs to be quoted.
    /// </summary>
    internal SearchValues<T> NeedsQuoting => field ??= InitNeedsQuoting();

    internal SearchValues<char> NeedsQuotingChar =>
        typeof(T) == typeof(char)
            ? (SearchValues<char>)(object)NeedsQuoting
            : (field ??= SearchValues.Create(_delimiter, _quote, '\r', '\n'));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SearchValues<T> InitNeedsQuoting()
    {
        Check.True(IsReadOnly, "Dialect must be read-only to cache NeedsQuoting");

        if (typeof(T) == typeof(byte))
        {
            return (SearchValues<T>)(object)SearchValues.Create((byte)_delimiter, (byte)_quote, (byte)'\r', (byte)'\n');
        }

        if (typeof(T) == typeof(char))
        {
            return (SearchValues<T>)(object)SearchValues.Create(_delimiter, _quote, '\r', '\n');
        }

        throw Token<T>.NotSupported;
    }

    internal bool DialectEqualsForWriting([NotNullWhen(true)] CsvOptions<T> other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // trimming not used by writer
        return Quote == other.Quote
            && Delimiter == other.Delimiter
            && Newline == other.Newline
            && FieldQuoting == other.FieldQuoting;
    }
}

// throwhelper doesn't need to be generic
file static class DialectHelper
{
    [StackTraceHidden]
    public static void ValidateToken(char value, [CallerArgumentExpression(nameof(value))] string name = "")
    {
        if (value > 127)
        {
            ThrowOutOfRange(name, value, "Dialect cannot contain non-ASCII characters (over 0x7F)");
        }

        if (value is '\0')
        {
            ThrowOutOfRange(name, value, "Dialect cannot contain null character");
        }

        if (value is '\r' or '\n')
        {
            ThrowOutOfRange(name, value, "Dialect cannot contain CR or LF due to newline ambiguity");
        }

        if (value is ' ')
        {
            ThrowOutOfRange(name, value, "Dialect cannot contain a space due to whitespace ambiguity");
        }

        if (char.IsAsciiLetterOrDigit(value) || value is '-')
        {
            ThrowOutOfRange(name, value, "Dialect cannot contain ASCII letters, numbers, or a minus sign");
        }
    }

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowOutOfRange(string name, char value, string message = "")
    {
        throw new ArgumentOutOfRangeException(
            paramName: nameof(value), // parameter name in the setter
            actualValue: value,
            message: $"Invalid {name}: {message}, value was: {GetToken(value)} (0x{(uint)value:X2})"
        );
    }

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowException(scoped ReadOnlySpan<string> errors, char delimiter, char quote)
    {
        throw new CsvConfigurationException(
            $"Invalid dialect configuration: {string.Join(" ", errors.ToArray())}. "
                + $"Delimiter: {GetToken(delimiter)}, Quote: {GetToken(quote)}"
        );
    }

    private static string GetToken(char? v)
    {
        return v switch
        {
            null => "<null>",
            '\0' => @"\0",
            '\r' => @"\r",
            '\n' => @"\n",
            '\t' => @"\t",
            '\v' => @"\v",
            '\f' => @"\f",
            ' ' => "' '",
            _ => v.Value.ToString(),
        };
    }
}
