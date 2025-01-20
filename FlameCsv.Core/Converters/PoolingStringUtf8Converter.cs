using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Converters;

internal sealed class PoolingStringUtf8Converter : CsvConverter<byte, string>
{
    private readonly StringPool _stringPool;

    public static PoolingStringUtf8Converter SharedInstance { get; } = new(CsvOptions<byte>.Default);

    public PoolingStringUtf8Converter(CsvOptions<byte> options) : this(options?.StringPool)
    {
    }

    public PoolingStringUtf8Converter(StringPool? stringPool)
    {
        _stringPool = stringPool ?? StringPool.Shared;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out string value)
    {
        int length = Encoding.UTF8.GetMaxCharCount(source.Length);

        if (Token<char>.CanStackalloc(length) ||
            Token<char>.CanStackalloc(length = Encoding.UTF8.GetCharCount(source)))
        {
            Span<char> buffer = stackalloc char[length];
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
