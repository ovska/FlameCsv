using System.Buffers.Text;
using System.Diagnostics;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Utf8;

/// <summary>
/// Parser for booleans.
/// </summary>
public sealed class BooleanUtf8Parser : ParserBase<byte, bool>, ICsvParserFactory<byte>
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

    internal BooleanUtf8Parser(string[] trueValues, string[] falseValues)
    {
        Debug.Assert(trueValues is { Length: > 0 });
        Debug.Assert(falseValues is { Length: > 0 });

        _values = new (ReadOnlyMemory<byte> bytes, bool value)[trueValues.Length + falseValues.Length];
        int index = 0;

        foreach (var value in trueValues)
            _values[index++] = (Encoding.UTF8.GetBytes(value ?? ""), true);

        foreach (var value in falseValues)
            _values[index++] = (Encoding.UTF8.GetBytes(value ?? ""), false);
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

    ICsvParser<byte> ICsvParserFactory<byte>.Create(Type resultType, CsvReaderOptions<byte> options)
    {
        var o = GuardEx.IsType<CsvUtf8ReaderOptions>(options);

        if (o.BooleanValues is { Count: > 0 } values)
            return new BooleanUtf8Parser(values);

        return this;
    }
}
