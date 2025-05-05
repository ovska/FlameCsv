using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using FlameCsv.Reading;

namespace FlameCsv.Extensions;

internal static class ReadExtensions
{
    public static bool TryParseFromUtf8<TValue>(
        ReadOnlySpan<byte> source,
        IFormatProvider? formatProvider,
        [MaybeNullWhen(false)] out TValue value)
        where TValue : ISpanParsable<TValue>
    {
        if (source.Length == 0) return TValue.TryParse([], formatProvider, out value);

        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);

        scoped Span<char> buffer;
        char[]? toReturn = null;

        if (Token<char>.CanStackalloc(maxLength) ||
            Token<char>.CanStackalloc(maxLength = Encoding.UTF8.GetCharCount(source)))
        {
            buffer = stackalloc char[maxLength];
        }
        else
        {
            buffer = toReturn = ArrayPool<char>.Shared.Rent(maxLength);
        }

        int written = Encoding.UTF8.GetChars(source, buffer);

        bool result = TValue.TryParse(buffer[..written], formatProvider, out value);

        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }

        return result;
    }

    public static bool TryFormatToUtf8<TValue>(
        Span<byte> destination,
        TValue value,
        string? format,
        IFormatProvider? formatProvider,
        out int charsWritten)
        where TValue : ISpanFormattable
    {
        Utf8.TryWriteInterpolatedStringHandler handler = new(
            literalLength: 0,
            formattedCount: 1,
            destination: destination,
            provider: formatProvider,
            shouldAppend: out bool shouldAppend);

        if (shouldAppend)
        {
            // the handler needs to be constructed by hand so we can pass in the dynamic format
            handler.AppendFormatted(value, format);
            return Utf8.TryWrite(destination, ref handler, out charsWritten);
        }

        charsWritten = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> Trim<T>(this ReadOnlySpan<T> value, CsvFieldTrimming trimming)
        where T : unmanaged, IBinaryInteger<T>
    {
        T space = T.CreateTruncating(' ');
        int start = 0;
        int end = value.Length - 1;

        if ((trimming & CsvFieldTrimming.Leading) != 0)
        {
            for (; start < value.Length; start++)
            {
                if (value[start] != space) break;
            }
        }

        if ((trimming & CsvFieldTrimming.Trailing) != 0)
        {
            for (; end >= start; end--)
            {
                if (value[end] != space) break;
            }
        }

        return value[start..(end + 1)];
    }
}
