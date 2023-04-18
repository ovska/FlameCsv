using FlameCsv.Extensions;

namespace FlameCsv.Formatters.Text;

public sealed class BooleanTextFormatter : ICsvFormatter<char, bool>
{
    public bool CanFormat(Type valueType) => valueType == typeof(bool);

    public bool TryFormat(bool value, Span<char> destination, out int tokensWritten)
    {
        return  (value ? "true" : "false").AsSpan().TryWriteTo(destination, out tokensWritten);
    }
}
