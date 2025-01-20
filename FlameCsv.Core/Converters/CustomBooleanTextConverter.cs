using System.Collections.Immutable;
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
        if (options._booleanValues is not { Count: > 0 })
            Throw.Argument(nameof(CsvOptions<char>.BooleanValues), "No values defined");

        _trueValues = [..options._booleanValues.Where(t => t.Item2).Select(t => t.Item1).Distinct(options.Comparer)];
        _falseValues = [..options._booleanValues.Where(t => !t.Item2).Select(t => t.Item1).Distinct(options.Comparer)];

        if (_trueValues.Length == 0) Throw.Config_TrueOrFalseBooleanValues(true);
        if (_falseValues.Length == 0) Throw.Config_TrueOrFalseBooleanValues(false);

        _firstTrue = _trueValues[0];
        _firstFalse = _falseValues[0];
        _comparer = FromOptions(options);
    }

    internal CustomBooleanTextConverter(
        string[] trueValues,
        string[] falseValues,
        StringComparison? comparison,
        CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(trueValues);
        ArgumentNullException.ThrowIfNull(falseValues);

        if (trueValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falseValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = [.. trueValues];
        _falseValues = [.. falseValues];
        _firstTrue = _trueValues[0];
        _firstFalse = _falseValues[0];
        _comparer = comparison is null
            ? FromOptions(options)
            : (IAlternateEqualityComparer<ReadOnlySpan<char>, string>)StringComparer.FromComparison(comparison.Value);
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

    private static IAlternateEqualityComparer<ReadOnlySpan<char>, string> FromOptions(CsvOptions<char> options)
    {
        if (options.Comparer is IAlternateEqualityComparer<ReadOnlySpan<char>, string> alternateComparer)
        {
            return alternateComparer;
        }

        throw new CsvConfigurationException(
            $"Comparer does not implement {nameof(IAlternateEqualityComparer<ReadOnlySpan<char>, string>)}.");
    }
}
