using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Exceptions;

#pragma warning disable CA1305 // Specify IFormatProvider

namespace FlameCsv.Reading.Unescaping;

internal struct IndexOfUnixUnescaper<T>(T escape, uint escapeCount) : IIndexOfUnescaper<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static int UnescapedLength(int fieldLength, uint specialCount)
    {
        return unchecked((int)((uint)fieldLength - specialCount));
    }

    public readonly bool AllSpecialConsumed => _escapeCount == 0;

    private uint _escapeCount = escapeCount;

    public int FindNext(ReadOnlySpan<T> value)
    {
        int index = value.IndexOf(escape);

        if (index >= 0)
        {
            _escapeCount--;

            if (index >= value.Length)
            {
                ThrowInvalidUnescape(value);
            }
        }

        return index;
    }

    public void ValidateState(ReadOnlySpan<T> field)
    {
        if (_escapeCount != 0)
        {
            ThrowInvalidUnescape(field);
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidUnescape(ReadOnlySpan<T> source)
    {
        var error = new StringBuilder(64);

        if (source.Length < 2)
        {
            error.Append($"Source is too short (length: {source.Length}). ");
        }
        else if (source[^1].Equals(escape))
        {
            error.Append("The source ended with an escape character. ");
        }

        int actualEscapeCount = source.Count(escape);
        if (actualEscapeCount != _escapeCount)
        {
            error.Append($"Escape character count {_escapeCount} was invalid (actual was {actualEscapeCount}). ");
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in source)
        {
            error.Append(token.Equals(escape) ? 'E' : 'x');
        }

        error.Append(']');

        throw new CsvFormatException($"Failed to unescape, invalid data: {error}");
    }
}
