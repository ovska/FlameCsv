using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Utf8;

/// <summary>
/// Parser that delegates UTF8 parsing to a text parser by converting the input into chars.
/// </summary>
public sealed class DelegatingUtf8Parser<TValue> : ParserBase<byte, TValue>
{
    public ICsvParser<char, TValue> Inner { get; }

    public DelegatingUtf8Parser(ICsvParser<char, TValue> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
    }

    public override bool TryParse(ReadOnlySpan<byte> span, out TValue value)
    {
        if (span.IsEmpty)
        {
            return Inner.TryParse(ReadOnlySpan<char>.Empty, out value!);
        }

        int maxLength = Encoding.UTF8.GetMaxCharCount(span.Length);

        if (maxLength <= 128)
        {
            Span<char> buffer = stackalloc char[maxLength];
            int written = Encoding.UTF8.GetChars(span, buffer);
            return Inner.TryParse(buffer[..written], out value!);
        }
        else
        {
            using var spanOwner = SpanOwner<char>.Allocate(maxLength);
            int written = Encoding.UTF8.GetChars(span, spanOwner.Span);
            return Inner.TryParse(spanOwner.Span[..written], out value!);
        }
    }
}
