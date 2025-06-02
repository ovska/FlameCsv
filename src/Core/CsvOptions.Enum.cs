using System.Diagnostics.CodeAnalysis;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    private string? _enumFormat;
    private char _enumFlagsSeparator = ',';
    private bool _ignoreEnumCase = true;
    private bool _allowUndefinedEnumValues;

    /// <summary>
    /// The default format for enums, used if enum's format is not defined in <see cref="Formats"/>.
    /// </summary>
    /// <seealso cref="UseDefaultConverters"/>
    [StringSyntax(StringSyntaxAttribute.EnumFormat)]
    public string? EnumFormat
    {
        get => _enumFormat;
        set
        {
            // validate
            _ = Enum.TryFormat(default(CsvFieldQuoting), default, out _, value);
            this.SetValue(ref _enumFormat, value);
        }
    }

    /// <summary>
    /// Whether to allow enum values that are not defined in the enum type.
    /// Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Enum.IsDefined"/>, flags-enums are considered valid if an undefined value is a
    /// combination of defined flags.
    /// </remarks>
    public bool AllowUndefinedEnumValues
    {
        get => _allowUndefinedEnumValues;
        set => this.SetValue(ref _allowUndefinedEnumValues, value);
    }

    /// <summary>
    /// Whether to ignore casing when parsing enum values. Default is <c>true</c>.
    /// </summary>
    public bool IgnoreEnumCase
    {
        get => _ignoreEnumCase;
        set => this.SetValue(ref _ignoreEnumCase, value);
    }

    /// <summary>
    /// Separator character used when parsing and formatting flags-enums as strings.<br/>
    /// Default is <c>','</c> (comma).
    /// </summary>
    /// <remarks>
    /// The separator character must not be a surrogate char, an ASCII digit, letter, or the '-' character.<br/>
    /// If an enum name contains the separator character, a runtime exception will be thrown.
    /// </remarks>
    public char EnumFlagsSeparator
    {
        get => _enumFlagsSeparator;
        set
        {
            ArgumentOutOfRangeException.ThrowIfZero(value);

            if (typeof(T) == typeof(char) && char.IsSurrogate(value))
            {
                Throw.Argument(nameof(value), "Surrogate characters are not allowed as enum separators.");
            }

            if (typeof(T) == typeof(byte) && !Ascii.IsValid(value))
            {
                Throw.Argument(nameof(value), "Only ASCII characters are allowed as enum separators for UTF8.");
            }

            if (value is ' ' or '-' || char.IsAsciiDigit(value) || char.IsAsciiLetter(value))
            {
                Throw.Argument(
                    nameof(value),
                    "To avoid ambiguity with numeric values, enum names, and whitespace, "
                        + "the enum flags separator cannot be an ASCII digit, letter, space, or the '-' character."
                );
            }

            this.SetValue(ref _enumFlagsSeparator, value);
        }
    }
}
