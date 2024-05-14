using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Converters;

internal sealed class PoolingStringUtf8Converter : CsvConverter<byte, string>
{
    public override bool HandleNull => true;

    private readonly StringPool _stringPool;

    public PoolingStringUtf8Converter(CsvOptions<byte> options) : this(options.StringPool)
    {
    }

    public PoolingStringUtf8Converter(StringPool? stringPool)
    {
        _stringPool = stringPool ?? StringPool.Shared;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out string value)
    {
        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);

        if (Token<char>.CanStackalloc(maxLength) ||
            Token<char>.CanStackalloc(maxLength = Encoding.UTF8.GetCharCount(source)))
        {
            Span<char> buffer = stackalloc char[maxLength];
            int written = Encoding.UTF8.GetChars(source, buffer);
            value = _stringPool.GetOrAdd(buffer[..written]);
        }
        else
        {
            value = _stringPool.GetOrAdd(source, Encoding.UTF8);
        }

        return true;
    }

    public override bool TryFormat(Span<byte> destination, string value, out int charsWritten)
    {
        return Encoding.UTF8.TryGetBytes(value, destination, out charsWritten);
    }
}
