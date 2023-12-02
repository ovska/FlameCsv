using System.Collections.Immutable;
using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class CustomBooleanTextConverter : CsvConverter<char, bool>
{
    private readonly ImmutableArray<string> _trueValues;
    private readonly ImmutableArray<string> _falseValues;
    private readonly StringComparison _comparison;

    internal CustomBooleanTextConverter(IList<(string text, bool value)> values)
    {
        Guard.IsNotEmpty(values);

        var trues = ImmutableArray.CreateBuilder<string>(values.Count);
        var falses = ImmutableArray.CreateBuilder<string>(values.Count);

        foreach ((string text, bool value) in values)
        {
            (value ? trues : falses).Add(text);
        }

        if (trues.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falses.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = trues.ToImmutable();
        _falseValues = falses.ToImmutable();
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
