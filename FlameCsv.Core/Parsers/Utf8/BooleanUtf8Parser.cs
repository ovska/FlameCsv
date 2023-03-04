using System.Buffers.Text;

namespace FlameCsv.Parsers.Utf8;

/// <summary>
/// Parser for booleans.
/// </summary>
public sealed class BooleanUtf8Parser : ParserBase<byte, bool>
{
    private readonly (ReadOnlyMemory<byte> bytes, bool value)[]? _values;

    /// <summary>
    /// Initializes an instance of <see cref="BooleanUtf8Parser"/>.
    /// </summary>
    /// <param name="values">
    /// Optional overrides for true/false values. If null or empty,
    /// <see cref="Utf8Parser.TryParse(ReadOnlySpan{byte}, out bool, out int, char)"/> is used.
    /// </param>
    public BooleanUtf8Parser(IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? values = null)
    {
        if (values is { Count: > 0 })
        {
            _values = values.ToArray();
        }
    }

    /// <inheritdoc/>
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
