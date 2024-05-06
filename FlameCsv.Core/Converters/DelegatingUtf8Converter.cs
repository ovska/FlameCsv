using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

public sealed class DelegatingUtf8Converter<TValue> : CsvConverter<byte, TValue>
{
    public override bool HandleNull => _converter.HandleNull;

    private readonly CsvOptions<char> _options;
    private readonly CsvConverter<char, TValue> _converter;

    public DelegatingUtf8Converter(
        CsvOptions<char> options,
        CsvConverter<char, TValue> converter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(converter);
        _options = options;
        _converter = converter;
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int charsWritten)
    {
        throw new NotImplementedException();

        Span<char> charBuffer = stackalloc char[Math.Min(256, destination.Length)];

        if (_converter.TryFormat(charBuffer, value, out int charsWritten2))
        {
            ReadOnlySpan<char> written = charBuffer[..charsWritten2];

            OperationStatus status = Ascii.FromUtf16(written, destination, out charsWritten);

            if (status == OperationStatus.Done)
                return true;

            if (status == OperationStatus.InvalidData)
                return Encoding.UTF8.TryGetBytes(written, destination, out charsWritten);
        }

        charsWritten = default;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        throw new NotImplementedException();
  
        scoped Span<char> charBuffer = stackalloc char[Math.Min(256, source.Length)];

        OperationStatus status = Ascii.ToUtf16(source, charBuffer, out int charsWritten);

        if (status == OperationStatus.Done)
        {
            return _converter.TryParse(charBuffer[..charsWritten], out value);
        }

        return TryParseSlow(source, charBuffer, out value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryParseSlow(
        ReadOnlySpan<byte> source,
        Span<char> charBuffer,
        [MaybeNullWhen(false)] out TValue value)
    {
        char[]? array = null;

        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);

        Span<char> buffer = maxLength <= charBuffer.Length
            ? charBuffer
            : (array = _options.ArrayPool.AllocatingIfNull().Rent(maxLength)).AsSpan();

        int written = Encoding.UTF8.GetChars(source, buffer);

        bool success = _converter.TryParse(buffer[..written], out value);

        if (array is not null)
            _options.ArrayPool.AllocatingIfNull().Return(array);

        return success;
    }
}
