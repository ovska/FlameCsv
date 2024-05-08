using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

public sealed class DelegatingUtf8Converter<TValue> : CsvConverter<byte, TValue>
{
    public override bool HandleNull => _converter.HandleNull;

    private readonly CsvConverter<char, TValue> _converter;
    private readonly string? _format;
    private readonly IFormatProvider? _provider;
    private readonly ArrayPool<char> _arrayPool;

    public DelegatingUtf8Converter(
        CsvTextOptions options,
        CsvConverter<char, TValue> converter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(converter);
        _converter = converter;
        _provider = options.FormatProvider;
        _format = options.DateOnlyFormat;
        _arrayPool = options.ArrayPool.AllocatingIfNull();
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int charsWritten)
    {
        Utf8.TryWriteInterpolatedStringHandler handler = new(
            literalLength: 0,
            formattedCount: 1,
            destination: destination,
            provider: _provider,
            shouldAppend: out bool shouldAppend);

        if (shouldAppend)
        {
            handler.AppendFormatted(value, _format);
            return Utf8.TryWrite(destination, _provider, ref handler, out charsWritten);
        }

        charsWritten = 0;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        int len = Encoding.UTF8.GetMaxCharCount(source.Length);

        if (Token<char>.CanStackalloc(len))
        {
            return TryParseImpl(source, stackalloc char[len], out value);
        }

        using var owner = SpanOwner<char>.Allocate(len, _arrayPool);
        return TryParseImpl(source, owner.Span, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryParseImpl(
        ReadOnlySpan<byte> source,
        scoped Span<char> charBuffer,
        [MaybeNullWhen(false)] out TValue value)
    {
        int written = Encoding.UTF8.GetChars(source, charBuffer);
        Debug.Assert(written <= charBuffer.Length);
        return _converter.TryParse(charBuffer[..written], out value);
    }
}
