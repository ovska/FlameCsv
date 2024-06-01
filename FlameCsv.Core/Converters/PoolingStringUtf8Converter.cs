using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class PoolingStringUtf8Converter : CsvConverter<byte, string>
{
    public override bool HandleNull => true;

    private readonly StringPool _stringPool;

    public static PoolingStringUtf8Converter SharedInstance { get; } = new(CsvUtf8Options.Default);

    private readonly ReadOnlyMemory<byte> _null;

    public PoolingStringUtf8Converter(CsvUtf8Options options)
    {
        _stringPool = options.StringPool ?? StringPool.Shared;

        if (options.NullTokens.TryGetValue(typeof(string), out var value))
            _null = value;
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
        if (value is null)
            return _null.Span.TryWriteTo(destination, out charsWritten);

        return Encoding.UTF8.TryGetBytes(value, destination, out charsWritten);
    }
}
