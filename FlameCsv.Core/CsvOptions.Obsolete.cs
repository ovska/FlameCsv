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
}
