using System.Buffers.Text;
using System.Diagnostics;
using System.Text;

namespace FlameCsv.Converters;

public sealed class BooleanUtf8Converter : CsvConverter<byte, bool>
{
    private readonly char _standardFormat;
    private readonly (ReadOnlyMemory<byte> bytes, bool value)[]? _values;

    /// <summary>
    /// Initializes an instance of <see cref="BooleanUtf8Parser"/>.
    /// </summary>
    /// <param name="values">
    /// Optional overrides for true/false values. If null or empty,
    /// <see cref="Utf8Parser.TryParse(ReadOnlySpan{byte}, out bool, out int, char)"/> is used.
    /// </param>
    public BooleanUtf8Converter(
        char standardFormat = default,
        IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? values = null)
    {
        _standardFormat = standardFormat;

        if (values is { Count: > 0 })
        {
            _values = values.ToArray();
        }
    }

    internal BooleanUtf8Converter(
        char standardFormat,
        string[] trueValues,
        string[] falseValues)
    {
        Debug.Assert(trueValues is { Length: > 0 });
        Debug.Assert(falseValues is { Length: > 0 });

        _standardFormat = standardFormat;

        _values = new (ReadOnlyMemory<byte> bytes, bool value)[trueValues.Length + falseValues.Length];
        int index = 0;

        foreach (var value in trueValues)
            _values[index++] = (Encoding.UTF8.GetBytes(value ?? ""), true);

        foreach (var value in falseValues)
            _values[index++] = (Encoding.UTF8.GetBytes(value ?? ""), false);
    }

    public override bool TryFormat(Span<byte> buffer, bool value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, buffer, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> span, out bool value)
    {
        if (_values is null)
        {
            return Utf8Parser.TryParse(span, out value, out int bytesConsumed, _standardFormat)
                && bytesConsumed == span.Length;
        }

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
