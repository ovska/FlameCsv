using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv;

internal readonly struct Dialect<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public readonly T Quote;
    public readonly T Escape;
    public readonly CsvFieldTrimming Trimming;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dialect(CsvOptions<T> options)
    {
        Quote = T.CreateTruncating(options.Quote);
        Escape = T.CreateTruncating(options.Escape.GetValueOrDefault());

        // remove undefined bits so we can cmp against zero later
        Trimming = options.Trimming & CsvFieldTrimming.Both;
    }
}

public partial class CsvOptions<T>
{
    private char _delimiter = ',';
    private char _quote = '"';
    private CsvNewline _newline = CsvNewline.CRLF;
    private char? _escape;
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
    /// Optional character used for escaping control characters.
    /// The default value is null, which means RFC4180 escaping (quotes) is used.
    /// </summary>
    public char? Escape
    {
        get => _escape;
        set
        {
            if (value.HasValue)
            {
                DialectHelper.ValidateToken(value.Value, nameof(value));
            }

            this.SetValue(ref _escape, value);
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
    /// Ensures <see cref="Delimiter"/>, <see cref="Quote"/>, and <see cref="Escape"/> are valid.
    /// </summary>
    /// <exception cref="CsvConfigurationException"/>
    public void Validate()
    {
        Debug.Assert(_delimiter is not ('\0' or '\r' or '\n' or ' '));
        Debug.Assert(_quote is not ('\0' or '\r' or '\n' or ' '));
        Debug.Assert(_escape is null or not ('\0' or '\r' or '\n' or ' '));

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

        if (_escape.HasValue)
        {
            if (_escape.GetValueOrDefault().Equals(_delimiter))
                errors.Append("Escape must not be equal to Delimiter.");

            if (_escape.GetValueOrDefault().Equals(_quote))
                errors.Append("Escape must not be equal to Quote.");
        }

        if (errors.Length != 0)
        {
            DialectHelper.ThrowException(errors.AsSpan(), _delimiter, _quote, _escape);
        }
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
        Debug.Assert(IsReadOnly, "Dialect must be read-only to cache NeedsQuoting");

        ReadOnlySpan<T> values =
        [
            T.CreateTruncating(_delimiter),
            T.CreateTruncating(_quote),
            T.CreateTruncating('\r'),
            T.CreateTruncating('\n'),
            T.CreateTruncating(_escape.GetValueOrDefault()),
        ];

        if (!Escape.HasValue)
        {
            values = values.Slice(0, 4);
        }

        if (typeof(T) == typeof(byte))
        {
            return (SearchValues<T>)(object)SearchValues.Create(MemoryMarshal.Cast<T, byte>(values));
        }

        if (typeof(T) == typeof(char))
        {
            return (SearchValues<T>)(object)SearchValues.Create(MemoryMarshal.Cast<T, char>(values));
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
            && FieldQuoting == other.FieldQuoting
            && Escape == other.Escape;
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
    public static void ThrowException(scoped ReadOnlySpan<string> errors, char delimiter, char quote, char? escape)
    {
        throw new CsvConfigurationException(
            $"Invalid dialect configuration: {string.Join(" ", errors.ToArray())}. "
                + $"Delimiter: {GetToken(delimiter)}, Quote: {GetToken(quote)}, Escape: {GetToken(escape.GetValueOrDefault())}"
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
