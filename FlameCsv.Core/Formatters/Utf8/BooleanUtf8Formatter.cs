using System.Buffers.Text;

namespace FlameCsv.Formatters.Utf8;

public sealed class BooleanUtf8Formatter : ICsvFormatter<byte, bool>
{
    public static BooleanUtf8Formatter Instance { get; } = new();

    public bool CanFormat(Type valueType) => valueType == typeof(bool);

    public bool TryFormat(bool value, Span<byte> destination, out int tokensWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out tokensWritten, format: 'l');
    }
}
