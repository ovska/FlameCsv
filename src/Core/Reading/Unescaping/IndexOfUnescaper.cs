using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv.Reading.Unescaping;

internal static class IndexOfUnescaper
{
    public static ReadOnlySpan<T> Unix<T>(ReadOnlySpan<T> field, CsvReader<T> reader, uint specialCount)
        where T : unmanaged, IBinaryInteger<T>
    {
        Debug.Assert(reader._dialect.Escape != default, "Escape character is not set");
        var unescaper = new IndexOfUnixUnescaper<T>(reader._dialect.Escape, specialCount);
        int unescapedLength = IndexOfUnixUnescaper<T>.UnescapedLength(field.Length, specialCount);
        Span<T> buffer = reader.GetUnescapeBuffer(unescapedLength);
        Field(field, unescaper, buffer);
        return buffer.Slice(0, unescapedLength);
    }

    public static void Field<T, TUnescaper>(ReadOnlySpan<T> field, TUnescaper unescaper, scoped Span<T> destination)
        where T : unmanaged, IBinaryInteger<T>
        where TUnescaper : struct, IIndexOfUnescaper<T>
    {
        int written = 0;
        int index = 0;

        while (index < field.Length)
        {
            int next = unescaper.FindNext(field.Slice(index));

            if (next < 0)
                break;

            if (next != 0)
            {
                field.Slice(index, next).CopyTo(destination.Slice(written));
                written += next;
            }

            next++;
            destination[written++] = field[next + index];
            index += next + 1; // advance past the escaped value

            // Found all quotes, copy the remaining data
            if (unescaper.AllSpecialConsumed)
            {
                field.Slice(index).CopyTo(destination.Slice(written));
                return;
            }
        }

        unescaper.ValidateState(field);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlySpan<T> Invalid<T>(ReadOnlySpan<T> value, uint field, byte quote)
        where T : unmanaged, IBinaryInteger<T>
    {
        string str;

        try
        {
            str = Transcode.ToString(value);
        }
        catch (Exception e)
        {
            str = "Failed to convert field to string: " + e.Message;
        }

        // TODO: LENIENCY
        string s = $"{Internal.Field.End(field)} (eol: {field >> 30:b} (quotes: {quote})";

        throw new CsvFormatException($"Cannot unescape invalid field {s}: '{str}'");
    }
}
