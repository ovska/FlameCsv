#if false
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures a string value to use for the enum instead of its' name when writing CSV.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class CsvEnumNameAttribute : Attribute
{
    /// <summary>
    /// Value of the enum when writing strings to CSV, or a possible value when reading.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Additional values that can be used to match the enum when reading CSV.
    /// <see cref="Value"/> is always used when writing.
    /// </summary>
    public ImmutableArray<string> Aliases { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvEnumNameAttribute"/> class.
    /// </summary>
    /// <param name="value">Value of the enum when writing strings to CSV, or a possible value when reading.</param>
    /// <param name="aliases">Additional values that can be used to match the enum when reading CSV.</param>
    /// <exception cref="ArgumentNullException">
    ///  The <paramref name="value"/> or <paramref name="aliases"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The <paramref name="value"/> is empty, starts with a digit or '-', or contains leading or trailing whitespace.
    /// </exception>
    public CsvEnumNameAttribute(string value, params string[] aliases)
    {
        ThrowIfInvalid(value);
        ArgumentNullException.ThrowIfNull(aliases);

        for (int i = 0; i < aliases.Length; i++)
        {
            ThrowIfInvalid(aliases[i]);
        }

        Value = value;
        Aliases = ImmutableCollectionsMarshal.AsImmutableArray(aliases);
    }

    [StackTraceHidden]
    private static void ThrowIfInvalid(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string paramName = "")
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        if (value.Length == 0 ||
            char.IsWhiteSpace(value[0]) ||
            char.IsWhiteSpace(value[^1]) ||
            char.IsAsciiDigit(value[0]) ||
            value[0] == '-')
        {
            Throw.Argument(
                paramName: paramName,
                message: "Enum name cannot be empty, start with a digit or '-', or contain leading or trailing whitespace.");
        }
    }
}
#endif
