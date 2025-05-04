using System.Collections.Immutable;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class CustomBooleanTextConverter : CsvConverter<char, bool>
{
    private readonly string _firstTrue;
    private readonly string _firstFalse;
    private readonly ImmutableArray<string> _trueValues;
    private readonly ImmutableArray<string> _falseValues;
    private readonly IAlternateEqualityComparer<ReadOnlySpan<char>, string> _comparer;

    internal CustomBooleanTextConverter(CsvOptions<char> options)
    {
        if (!options.HasBooleanValues)
            Throw.Argument(nameof(CsvOptions<char>.BooleanValues), "No values defined");

        // ReSharper disable once SuspiciousTypeConversion.Global
        _comparer = options.Comparer as IAlternateEqualityComparer<ReadOnlySpan<char>, string> ??
            throw new CsvConfigurationException(
                $"Comparer does not implement {nameof(IAlternateEqualityComparer<ReadOnlySpan<char>, string>)}.");

        _trueValues = [.. options.BooleanValues.Where(t => t.Item2).Select(t => t.Item1).Distinct(options.Comparer)];
        _falseValues = [.. options.BooleanValues.Where(t => !t.Item2).Select(t => t.Item1).Distinct(options.Comparer)];

        if (_trueValues.Length == 0) Throw.Config_TrueOrFalseBooleanValues(true);
        if (_falseValues.Length == 0) Throw.Config_TrueOrFalseBooleanValues(false);

        _firstTrue = _trueValues[0];
        _firstFalse = _falseValues[0];
    }

    internal CustomBooleanTextConverter(
        string[] trueValues,
        string[] falseValues,
        bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(trueValues);
        ArgumentNullException.ThrowIfNull(falseValues);

        if (trueValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falseValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = ImmutableCollectionsMarshal.AsImmutableArray(trueValues);
        _falseValues = ImmutableCollectionsMarshal.AsImmutableArray(falseValues);
        _firstTrue = _trueValues[0];
        _firstFalse = _falseValues[0];
        _comparer = (IAlternateEqualityComparer<ReadOnlySpan<char>, string>)
            (ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    public override bool TryFormat(Span<char> destination, bool value, out int charsWritten)
    {
        return (value ? _firstTrue : _firstFalse).AsSpan().TryCopyTo(destination, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<char> source, out bool value)
    {
        foreach (string v in _trueValues.AsSpan())
        {
            if (_comparer.Equals(source, v))
            {
                value = true;
                return true;
            }
        }

        foreach (string v in _falseValues.AsSpan())
        {
            if (_comparer.Equals(source, v))
            {
                value = false;
                return true;
            }
        }

        return value = false;
    }
}
