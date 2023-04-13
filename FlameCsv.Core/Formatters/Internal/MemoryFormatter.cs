using FlameCsv.Extensions;

namespace FlameCsv.Formatters.Internal;

internal sealed class MemoryFormatter<T> : ICsvFormatter<T, ReadOnlyMemory<T>>
    where T : unmanaged, IEquatable<T>
{
    public static MemoryFormatter<T> Instance => _instance ??= new MemoryFormatter<T>();

    private static MemoryFormatter<T>? _instance;

    public bool CanFormat(Type valueType) => valueType == typeof(ReadOnlyMemory<T>);

    public bool TryFormat(ReadOnlyMemory<T> value, Span<T> destination, out int tokensWritten)
    {
        return value.Span.TryWriteTo(destination, out tokensWritten);
    }
}
