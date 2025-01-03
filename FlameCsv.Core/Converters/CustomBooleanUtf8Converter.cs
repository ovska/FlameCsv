using System.Diagnostics;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class CustomBooleanUtf8Converter : CsvConverter<byte, bool>
{
    private readonly byte[][] _trueValues;
    private readonly byte[][] _falseValues;

    internal CustomBooleanUtf8Converter(CsvOptions<byte> options)
    {
        if (options._booleanValues is not { Count: > 0 })
            Throw.Argument(nameof(CsvOptions<byte>.BooleanValues), "No values defined");

        var values = options._booleanValues;

        List<byte[]> trues = new((values.Count / 2) + 1);
        List<byte[]> falses = new((values.Count / 2) + 1);

        foreach ((string text, bool value) in values)
        {
            (value ? trues : falses).Add(Encoding.UTF8.GetBytes(text ?? ""));
        }

        if (trues.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falses.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = [.. trues];
        _falseValues = [.. falses];
    }

    internal CustomBooleanUtf8Converter(string[] trueValues, string[] falseValues)
    {
        Debug.Assert(trueValues is { Length: > 0 });
        Debug.Assert(falseValues is { Length: > 0 });

        _trueValues = trueValues.Select(v => Encoding.UTF8.GetBytes(v ?? "")).ToArray();
        _falseValues = falseValues.Select(v => Encoding.UTF8.GetBytes(v ?? "")).ToArray();
    }

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        return (value ? _trueValues : _falseValues)[0].AsSpan().TryWriteTo(destination, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        foreach (byte[] v in _trueValues.AsSpan())
        {
            if (source.SequenceEqual(v.AsSpan()))
            {
                value = true;
                return true;
            }
        }

        foreach (byte[] v in _falseValues.AsSpan())
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
