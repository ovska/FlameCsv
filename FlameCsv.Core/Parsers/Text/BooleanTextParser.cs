using CommunityToolkit.Diagnostics;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for booleans.
/// </summary>
public sealed class BooleanTextParser : ParserBase<char, bool>
{
    private readonly (string text, bool value)[]? _values;

    /// <summary>
    /// Initializes an instance of <see cref="BooleanTextParser"/>.
    /// </summary>
    /// <param name="values">
    /// Optional overrides for true/false values. If null or empty, <see cref="bool.TryParse(ReadOnlySpan{char}, out bool)"/>
    /// is used.
    /// </param>
    public BooleanTextParser(
        IReadOnlyCollection<(string text, bool value)>? values = null)
    {
        if (values is { Count: > 0 })
        {
            _values = values.ToArray();
        }
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
}
