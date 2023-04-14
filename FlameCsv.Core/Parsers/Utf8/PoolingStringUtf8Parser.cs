using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Utf8;

internal class PoolingStringUtf8Parser :
    ICsvParser<byte, string>,
    ICsvParser<byte, ReadOnlyMemory<char>>
{
    public StringPool StringPool { get; }

    public PoolingStringUtf8Parser() : this(StringPool.Shared)
    {
    }

    public PoolingStringUtf8Parser(StringPool stringPool)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        StringPool = stringPool;
    }

    public bool TryParse(ReadOnlySpan<byte> span, [MaybeNullWhen(false)] out string value)
    {
        value = !span.IsEmpty ? Impl(span) : "";
        return true;
    }

    public bool TryParse(ReadOnlySpan<byte> span, [MaybeNullWhen(false)] out ReadOnlyMemory<char> value)
    {
        value = !span.IsEmpty ? Impl(span).AsMemory() : default;
        return true;
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(string) || resultType == typeof(ReadOnlyMemory<char>);
    }

    private string Impl(ReadOnlySpan<byte> span)
    {
        int maxLength = Encoding.UTF8.GetMaxCharCount(span.Length);

        if (Token<char>.CanStackalloc(maxLength))
        {
            Span<char> buffer = stackalloc char[maxLength];
            int written = Encoding.UTF8.GetChars(span, buffer);
            return StringPool.GetOrAdd(buffer[..written]);
        }

        return StringPool.GetOrAdd(span, Encoding.UTF8);
    }
}
