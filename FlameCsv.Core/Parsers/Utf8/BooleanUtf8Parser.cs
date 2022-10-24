using System.Buffers.Text;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Parsers.Utf8;

public sealed class BooleanUtf8Parser : ParserBase<byte, bool>
{
    private readonly (ReadOnlyMemory<byte> bytes, bool value)[]? _values;

    public BooleanUtf8Parser(IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? values = null)
    {
        if (values is not null)
        {
            Guard.IsNotEmpty(values);
            _values = values.ToArray();
        }
    }

    public override bool TryParse(ReadOnlySpan<byte> span, out bool value)
    {
        if (_values is null)
            return Utf8Parser.TryParse(span, out value, out _);

        foreach (ref var tuple in _values.AsSpan())
        {
            if (span.SequenceEqual(tuple.bytes.Span))
            {
                value = tuple.value;
                return true;
            }
        }

        return value = false;
    }
}
