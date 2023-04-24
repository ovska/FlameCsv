using System.Reflection;

namespace FlameCsv.Formatters.Text;

public sealed class EnumTextFormatter<TEnum> : ICsvFormatter<char, TEnum>
    where TEnum : struct, Enum
{
    public bool CanFormat(Type valueType)
    {
        return valueType.IsEnum && valueType.GetCustomAttribute<FlagsAttribute>(inherit: false) is null;
    }

    public bool TryFormat(TEnum value, Span<char> destination, out int tokensWritten)
    {
        throw new NotImplementedException();
    }
}
