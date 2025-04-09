using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Exceptions;

namespace FlameCsv.Reading.Unescaping;

internal struct IndexOfRFC4180Unescaper<T>(T quote, uint quoteCount) : IIndexOfUnescaper<T> where T : unmanaged, IBinaryInteger<T>
{
    public static int UnescapedLength(int fieldLength, uint specialCount)
    {
        return fieldLength - unchecked((int)(specialCount / 2));
    }

    public bool AllSpecialConsumed => _quoteCount == 0;

    private uint _quoteCount = quoteCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindNext(ReadOnlySpan<T> value)
    {
        int index = value.IndexOf(quote);

        if (index >= 0)
        {
            if (index >= value.Length || value[index + 1] != quote)
            {
                ThrowInvalidUnescape(value);
            }

            _quoteCount -= 2;
        }

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ValidateState(ReadOnlySpan<T> field)
    {
        if (_quoteCount == 0)
            return;

        ThrowInvalidUnescape(field);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    void ThrowInvalidUnescape(ReadOnlySpan<T> field)
    {
        int actualCount = field.Count(quote);

        var error = new StringBuilder(64);

        if (field.Length < 2)
        {
            error.Append(CultureInfo.InvariantCulture, $"Source is too short (length: {field.Length}). ");
        }

        if (actualCount != _quoteCount)
        {
            error.Append(
                CultureInfo.InvariantCulture,
                $"String delimiter count {_quoteCount} was invalid (actual was {actualCount}). ");
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in field)
        {
            error.Append(token.Equals(quote) ? '"' : 'x');
        }

        error.Append(']');

        // TODO: LENIENCY
        throw new CsvFormatException($"Cannot unescape invalid field: {error}");
    }
}
