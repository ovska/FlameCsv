using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv;

// TODO: figure out if these should be obsoleted, or left for convenience

public partial class CsvOptions<T>
{
    /// <summary>
    /// Whether to allow enum values that are not defined in the enum type.
    /// Default is <see langword="false"/>.
    /// </summary>
    /// <seealso cref="EnumOptions"/>
    public bool AllowUndefinedEnumValues
    {
        get => (_enumFlags & CsvEnumOptions.AllowUndefinedValues) != 0;
        set
        {
            this.ThrowIfReadOnly();

            if (value)
            {
                _enumFlags |= CsvEnumOptions.AllowUndefinedValues;
            }
            else
            {
                _enumFlags &= ~CsvEnumOptions.AllowUndefinedValues;
            }
        }
    }

    /// <summary>
    /// Whether to ignore casing when parsing enum values. Default is <see langword="true"/>.
    /// </summary>
    /// <seealso cref="EnumOptions"/>
    public bool IgnoreEnumCase
    {
        get => (_enumFlags & CsvEnumOptions.IgnoreCase) != 0;
        set
        {
            this.ThrowIfReadOnly();

            if (value)
            {
                _enumFlags |= CsvEnumOptions.IgnoreCase;
            }
            else
            {
                _enumFlags &= ~CsvEnumOptions.IgnoreCase;
            }
        }
    }

    /// <summary>
    /// Separator character used when parsing and formatting flags-enums as strings.
    /// </summary>
    public char EnumFlagsSeparator
    {
        get => _enumFlagsSeparator;
        set
        {
            ArgumentOutOfRangeException.ThrowIfZero(value);

            if (char.IsSurrogate(value))
            {
                Throw.Argument(nameof(value), "Surrogate characters are not allowed as enum separators.");
            }

            if (typeof(T) == typeof(byte) && !Ascii.IsValid(value))
            {
                Throw.Argument(nameof(value), "Only ASCII characters are allowed as enum separators for UTF8.");
            }

            if (char.IsAsciiDigit(value) || char.IsAsciiLetter(value) || value == '-')
            {
                Throw.Argument(
                    nameof(value),
                    "Enum flags separator cannot be an ASCII digit, letter, or the '-' character.");
            }

            this.ThrowIfReadOnly();
            _enumFlagsSeparator = value;
        }
    }
}
