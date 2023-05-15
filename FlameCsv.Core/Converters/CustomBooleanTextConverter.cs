using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class CustomBooleanTextConverter : CsvConverter<char, bool>
{
    private readonly string[] _trueValues;
    private readonly string[] _falseValues;

    internal CustomBooleanTextConverter(IList<(string text, bool value)> values)
    {
        Guard.IsNotEmpty(values);

        List<string> trues = new(values.Count / 2 + 1);
        List<string> falses = new(values.Count / 2 + 1);

        foreach ((string text, bool value) in values)
        {
            (value ? trues : falses).Add(text);
        }

        if (trues.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falses.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = trues.ToArray();
        _falseValues = falses.ToArray();
    }

    internal CustomBooleanTextConverter(string[] trueValues, string[] falseValues)
    {
        ArgumentNullException.ThrowIfNull(trueValues);
        ArgumentNullException.ThrowIfNull(falseValues);

        if (trueValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falseValues.Length == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = trueValues;
        _falseValues = falseValues;
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
            if (source.SequenceEqual(v.AsSpan()))
            {
                value = true;
                return true;
            }
        }

        foreach (string v in _falseValues.AsSpan())
        {
            if (source.SequenceEqual(v.AsSpan()))
            {
                value = false;
                return true;
            }
        }

        return value = false;
    }
}
