using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class StringUtf8Converter : CsvConverter<byte, string>
{
    public override bool HandleNull => true;

    public static StringUtf8Converter Instance { get; } = new(CsvUtf8Options.Default);

    private readonly ReadOnlyMemory<byte> _null;

    public StringUtf8Converter(CsvUtf8Options options)
    {
        if (options.NullTokens.TryGetValue(typeof(string), out var value))
            _null = value;
    }

    public override bool TryFormat(Span<byte> destination, string value, out int charsWritten)
    {
        if (value is null)
            return _null.Span.TryWriteTo(destination, out charsWritten);

        return Encoding.UTF8.TryGetBytes(value, destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out string value)
    {
        value = Encoding.UTF8.GetString(source);
        return true;
    }
}
