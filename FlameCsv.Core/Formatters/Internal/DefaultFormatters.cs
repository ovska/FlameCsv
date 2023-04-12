using FlameCsv.Binding;
using FlameCsv.Formatters.Text;
using FlameCsv.Formatters.Utf8;

namespace FlameCsv.Formatters.Internal;

internal static class DefaultFormatters
{
    public static ICsvFormatter<T, CsvBinding<TValue>> Binding<T, TValue>()
        where T : unmanaged, IEquatable<T>
    {
        if (typeof(T) == typeof(char))
            return (ICsvFormatter<T, CsvBinding<TValue>>)(object)BindingTextFormatter<TValue>.Instance;

        if (typeof(T) == typeof(byte))
            return (ICsvFormatter<T, CsvBinding<TValue>>)(object)BindingUtf8Formatter<TValue>.Instance;

        throw new NotSupportedException($"Built-in header formatter for {typeof(T)} is not supported.");
    }

    public static ICsvFormatter<T, string> String<T>()
        where T : unmanaged, IEquatable<T>
    {
        if (typeof(T) == typeof(char))
            return (ICsvFormatter<T, string>)(object)StringTextFormatter.Instance;

        if (typeof(T) == typeof(byte))
            return (ICsvFormatter<T, string>)(object)StringUtf8Formatter.Instance;

        throw new NotSupportedException($"Built-in string formatter for {typeof(T)} is not supported.");
    }
}
