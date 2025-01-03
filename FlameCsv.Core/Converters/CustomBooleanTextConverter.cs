using System.Collections.Immutable;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class CustomBooleanTextConverter : CsvConverter<char, bool>
{
    private readonly ImmutableArray<string> _trueValues;
    private readonly ImmutableArray<string> _falseValues;
    private readonly StringComparison _comparison;

    internal CustomBooleanTextConverter(CsvOptions<char> options)
    {
        if (options._booleanValues is not { Count: > 0 })
            Throw.Argument(nameof(CsvOptions<char>.BooleanValues), "No values defined");

        var values = options._booleanValues;
        var trueValues = ImmutableArray.CreateBuilder<string>(values.Count);
        var falseValues = ImmutableArray.CreateBuilder<string>(values.Count);

        foreach ((string text, bool value) in values)
        {
            (value ? trueValues : falseValues).Add(text);
        }

        if (trueValues.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falseValues.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = trueValues.ToImmutable();
        _falseValues = falseValues.ToImmutable();
        _comparison = StringComparison.OrdinalIgnoreCase;
    }

    internal CustomBooleanTextConverter(string[] trueValues, string[] falseValues, StringComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(trueValues);
        ArgumentNullException.ThrowIfNull(falseValues);

        if (trueValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falseValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        // validate comparison
        _ = string.Equals(trueValues[0], falseValues[0], comparison);

        _trueValues = [.. trueValues];
        _falseValues = [.. falseValues];
        _comparison = comparison;
    }

    public override bool TryFormat(Span<char> destination, bool value, out int charsWritten)
    {
        return (value ? _trueValues : _falseValues)[0].AsSpan().TryWriteTo(destination, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<char> source, out bool value)
    {
        foreach (string v in _trueValues.AsSpan())
        {
            if (source.Equals(v.AsSpan(), _comparison))
            {
                value = true;
                return true;
            }
        }

        foreach (string v in _falseValues.AsSpan())
        {
            if (source.Equals(v.AsSpan(), _comparison))
            {
                value = false;
                return true;
            }
        }

        return value = false;
    }
}
