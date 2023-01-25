using FlameCsv.Extensions;

namespace FlameCsv.Formatters.Text;

public sealed class EnumTextFormatter<TEnum> : ICsvFormatter<char, TEnum>
    where TEnum : struct, Enum
{
    public bool CanFormat(Type resultType)
    {
        return resultType.IsEnum && !resultType.HasAttribute<FlagsAttribute>();
    }

    public bool TryFormat(TEnum value, Span<char> destination, out int tokensWritten)
    {
        throw new NotImplementedException();
    }
}
