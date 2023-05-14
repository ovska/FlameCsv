using System.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

/// <summary>
/// Parser for booleans.
/// </summary>
public sealed class BooleanTextConverter : CsvConverter<char, bool>
{
    private readonly (string text, bool value)[]? _values;
    private readonly string? _trueText;
    private readonly string? _falseText;

    /// <summary>
    /// Initializes an instance of <see cref="BooleanTextConverter"/>.
    /// </summary>
    /// <param name="values">
    /// Optional overrides for true/false values. If null or empty, <see cref="bool.TryParse(ReadOnlySpan{char}, out bool)"/>
    /// is used.
    /// </param>
    public BooleanTextConverter(
        IReadOnlyCollection<(string text, bool value)>? values = null)
    {
        if (values is { Count: > 0 })
        {
            _values = values.ToArray();
            InitTrueFalseText(out _trueText, out _falseText);
        }
    }

    internal BooleanTextConverter(string[] trueValues, string[] falseValues)
    {
        Debug.Assert(trueValues is { Length: > 0 });
        Debug.Assert(falseValues is { Length: > 0 });

        _values = new (string text, bool value)[trueValues.Length + falseValues.Length];
        int index = 0;

        foreach (var value in trueValues)
            _values[index++] = (value, true);

        foreach (var value in falseValues)
            _values[index++] = (value, false);

        InitTrueFalseText(out _trueText, out _falseText);
    }

    public override bool TryFormat(Span<char> buffer, bool value, out int charsWritten)
    {
        if (_values is not null)
        {
            if (value)
            {
                if (_trueText is not null)
                    return _trueText.AsSpan().TryWriteTo(buffer, out charsWritten);
            }
            else
            {
                if (_falseText is not null)
                    return _falseText.AsSpan().TryWriteTo(buffer, out charsWritten);
            }
        }

        return value.TryFormat(buffer, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<char> span, out bool value)
    {
        if (_values is null)
            return bool.TryParse(span, out value);

        foreach (ref var tuple in _values.AsSpan())
        {
            if (span.SequenceEqual(tuple.text.AsSpan()))
            {
                value = tuple.value;
                return true;
            }
        }

        return value = false;
    }

    private void InitTrueFalseText(out string? trueText, out string? falseText)
    {
        trueText = falseText = null;

        if (_values is null)
            return;

        foreach (var (text, value) in _values)
        {
            if (value)
            {
                trueText ??= text;
            }
            else
            {
                falseText ??= text;
            }
        }
    }
}
