using CommunityToolkit.Diagnostics;

namespace FlameCsv.Parsers.Text;

public sealed class BooleanTextParser : ParserBase<char, bool>
{
    private readonly (string text, bool value)[]? _values;

    public BooleanTextParser(
        IReadOnlyCollection<(string text, bool value)>? values = null)
    {
        if (values is not null)
        {
            Guard.IsNotEmpty(values);
            _values = values.ToArray();
        }
    }

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
