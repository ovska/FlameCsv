using System.Buffers.Text;
using System.Diagnostics;
using System.Text;

namespace FlameCsv.Converters;

internal sealed class BooleanUtf8Converter : CsvConverter<byte, bool>
{
    private readonly char _standardFormat;
    private readonly (ReadOnlyMemory<byte> bytes, bool value)[]? _values;

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

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        if (_values is null)
        {
            return Utf8Parser.TryParse(source, out value, out int bytesConsumed, _standardFormat)
                && bytesConsumed == source.Length;
        }

        foreach (ref var tuple in _values.AsSpan())
        {
            if (source.SequenceEqual(tuple.bytes.Span))
            {
                value = tuple.value;
                return true;
            }
        }

        return value = false;
    }
}
