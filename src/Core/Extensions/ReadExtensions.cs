using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Extensions;

internal static class ReadExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRecordLength(this ReadOnlySpan<uint> fields, bool isFirst, bool includeTrailingNewline = false)
    {
        uint lastField = fields[^1];
        uint firstField = fields[0];
        return GetRecordLength(firstField, lastField, isFirst, includeTrailingNewline);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRecordLength(uint first, uint last, bool isFirst, bool includeTrailingNewline = false)
    {
        int end = includeTrailingNewline ? Field.NextStart(last) : Field.End(last);
        int start = isFirst ? 0 : Field.NextStart(first);
        return end - start;
    }

    public static bool TryParseFromUtf8<TValue>(
        ReadOnlySpan<byte> source,
        IFormatProvider? formatProvider,
        [MaybeNullWhen(false)] out TValue value
    )
        where TValue : ISpanParsable<TValue>
    {
        if (source.Length == 0)
            return TValue.TryParse([], formatProvider, out value);

        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);

        scoped Span<char> buffer;
        char[]? toReturn = null;

        if (
            Token<char>.CanStackalloc(maxLength)
            || Token<char>.CanStackalloc(maxLength = Encoding.UTF8.GetCharCount(source))
        )
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
        out int charsWritten
    )
        where TValue : ISpanFormattable
    {
        // the handler needs to be constructed by hand so we can pass in the dynamic format
        Utf8.TryWriteInterpolatedStringHandler handler = new(
            literalLength: 0,
            formattedCount: 1,
            destination: destination,
            provider: formatProvider,
            shouldAppend: out _
        );

        handler.AppendFormatted(value, format);
        return Utf8.TryWrite(destination, ref handler, out charsWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsTrimming<T>(this ReadOnlySpan<T> value, CsvFieldTrimming trimming)
        where T : unmanaged, IBinaryInteger<T>
    {
        return value.Length > 0 & (trimming & CsvFieldTrimming.Both) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TrimUnsafe<T>(
        this CsvFieldTrimming trimming,
        scoped ref T data,
        scoped ref int start,
        scoped ref int end
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        Debug.Assert(start >= 0 && end >= start);

        T space = T.CreateTruncating(' ');

        if ((trimming & CsvFieldTrimming.Leading) != 0 && start < end && Unsafe.Add(ref data, (uint)start) == space)
        {
            for (start++; start < end; start++)
            {
                if (Unsafe.Add(ref data, (uint)start) != space)
                    break;
            }
        }

        if ((trimming & CsvFieldTrimming.Trailing) != 0 && end > start && Unsafe.Add(ref data, (uint)end - 1u) == space)
        {
            for (end--; end > start; end--)
            {
                if (Unsafe.Add(ref data, (uint)end - 1u) != space)
                    break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> Trim<T>(this ReadOnlySpan<T> value, CsvFieldTrimming trimming)
        where T : unmanaged, IBinaryInteger<T>
    {
        int start = 0;
        int end = value.Length - 1;

        if ((trimming & CsvFieldTrimming.Leading) != 0)
        {
            for (; start < value.Length; start++)
            {
                if (value[start] != T.CreateTruncating(' '))
                    break;
            }
        }

        if ((trimming & CsvFieldTrimming.Trailing) != 0)
        {
            for (; end >= start; end--)
            {
                if (value[end] != T.CreateTruncating(' '))
                    break;
            }
        }

        return value[start..(end + 1)];
    }
}
