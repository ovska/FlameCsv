using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Unicode;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Extensions;

internal static class ReadExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint MoveMask<T>(this Vector<T> vector)
    {
        return (Vector<byte>.Count * 8) switch
        {
            128 => vector.AsVector128().ExtractMostSignificantBits(),
            256 => vector.AsVector256().ExtractMostSignificantBits(),
            512 when nuint.Size is 8 => (nuint)vector.AsVector512().ExtractMostSignificantBits(),
            int s => throw new NotSupportedException($"Unsupported vector size {s}."),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRecordLength(this ReadOnlySpan<uint> fields, bool includeTrailingNewline = false)
    {
        // TODO OPTIMIZE?
        uint lastField = fields[^1];
        uint firstField = fields[0];

        int end = includeTrailingNewline ? Field.NextStart(lastField) : Field.End(lastField);
        int start = Field.NextStart(firstField);

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
        T space;

        if (typeof(T) == typeof(byte))
        {
            space = Unsafe.BitCast<byte, T>((byte)' ');
        }
        else if (typeof(T) == typeof(char))
        {
            space = Unsafe.BitCast<char, T>(' ');
        }
        else
        {
            space = T.CreateTruncating(' ');
        }

        if ((trimming & CsvFieldTrimming.Leading) != 0)
        {
            for (; start < end; start++)
            {
                if (Unsafe.Add(ref data, start) != space)
                    break;
            }
        }

        if ((trimming & CsvFieldTrimming.Trailing) != 0)
        {
            for (; end > start; end--)
            {
                if (Unsafe.Add(ref data, end - 1) != space)
                    break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> Trim<T>(this ReadOnlySpan<T> value, CsvFieldTrimming trimming)
        where T : unmanaged, IBinaryInteger<T>
    {
        T space;

        if (typeof(T) == typeof(byte))
        {
            space = Unsafe.BitCast<byte, T>((byte)' ');
        }
        else if (typeof(T) == typeof(char))
        {
            space = Unsafe.BitCast<char, T>(' ');
        }
        else
        {
            space = T.CreateTruncating(' ');
        }

        int start = 0;
        int end = value.Length - 1;

        if ((trimming & CsvFieldTrimming.Leading) != 0)
        {
            for (; start < value.Length; start++)
            {
                if (value[start] != space)
                    break;
            }
        }

        if ((trimming & CsvFieldTrimming.Trailing) != 0)
        {
            for (; end >= start; end--)
            {
                if (value[end] != space)
                    break;
            }
        }

        return value[start..(end + 1)];
    }
}
