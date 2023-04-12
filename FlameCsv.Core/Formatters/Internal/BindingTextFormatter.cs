using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Binding;
using FlameCsv.Binding.Internal;
using FlameCsv.Extensions;

namespace FlameCsv.Formatters.Internal;

internal sealed class BindingTextFormatter<T> : ICsvFormatter<char, CsvBinding<T>>
{
    public static BindingTextFormatter<T> Instance => _instance ??= new();

    private static BindingTextFormatter<T>? _instance;

    public bool CanFormat(Type valueType) => valueType == typeof(MemberCsvBinding<T>);

    public bool TryFormat(CsvBinding<T> value, Span<char> destination, out int tokensWritten)
    {
        return ((MemberCsvBinding<T>)value).Member.Name.AsSpan().TryWriteTo(destination, out tokensWritten);
    }
}

internal sealed class BindingUtf8Formatter<T> : ICsvFormatter<byte, CsvBinding<T>>
{
    public static BindingUtf8Formatter<T> Instance => _instance ??= new();

    private static BindingUtf8Formatter<T>? _instance;

    public bool CanFormat(Type valueType) => valueType == typeof(MemberCsvBinding<T>);

    public bool TryFormat(CsvBinding<T> value, Span<byte> destination, out int tokensWritten)
    {
        var memberName = ((MemberCsvBinding<T>)value).Member.Name.AsSpan();

        var maxLength = Encoding.UTF8.GetMaxByteCount(memberName.Length);

        if (Token<byte>.CanStackalloc(maxLength))
        {
            Span<byte> buffer = stackalloc byte[maxLength];
            int encoded = Encoding.UTF8.GetBytes(memberName, buffer);
            return buffer[..encoded].TryWriteTo(destination, out tokensWritten);
        }
        else
        {
            using var spanOwner = SpanOwner<byte>.Allocate(maxLength);
            int encoded = Encoding.UTF8.GetBytes(memberName, spanOwner.Span);
            return spanOwner.Span[..encoded].TryWriteTo(destination, out tokensWritten);
        }
    }
}
