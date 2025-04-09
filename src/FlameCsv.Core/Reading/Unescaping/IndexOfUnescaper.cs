using System.Diagnostics.CodeAnalysis;
using FlameCsv.Exceptions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading.Unescaping;

internal static class IndexOfUnescaper
{
    public static void Field<T, TUnescaper>(
        ReadOnlySpan<T> field,
        TUnescaper unescaper,
        scoped Span<T> destination)
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
    public static void Invalid<T>(ReadOnlySpan<T> field, ref readonly Meta meta) where T : unmanaged, IBinaryInteger<T>
    {
        string str;

        try
        {
            str = CsvOptions<T>.Default.GetAsString(field);
        }
        catch (Exception e)
        {
            str = "Failed to convert field to string: " + e.Message;
        }

        // TODO: LENIENCY
        throw new CsvFormatException($"Cannot unescape invalid field {meta}: {str}");
    }
}
