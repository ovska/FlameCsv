using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

internal static class Unescape
{
    [DoesNotReturn]
    public static void Invalid<T>(ReadOnlySpan<T> field, ref readonly Meta meta) where T : unmanaged, IBinaryInteger<T>
    {
        string str = "";

        try
        {
            str = CsvOptions<T>.Default.GetAsString(field);
        }
        catch (Exception e)
        {
            str = "Failed to convert field to string: " + e.Message;
        }

        throw new UnreachableException($"Cannot escape invalid field with meta {meta}: {str}");
    }

    public static void Field<T, TUnescaper>(
        ReadOnlySpan<T> field,
        TUnescaper unescaper,
        scoped Span<T> destination)
        where T : unmanaged, IBinaryInteger<T>
        where TUnescaper : struct, IUnescaper<T>
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

            // Found all quotes, copy remaining data
            if (unescaper.AllSpecialConsumed)
            {
                field.Slice(index).CopyTo(destination.Slice(written));
                // written += source.Length - index;
                return;
            }
        }

        unescaper.ValidateState(field);
    }
}

internal interface IUnescaper<T> where T : unmanaged, IBinaryInteger<T>
{
    static abstract int UnescapedLength(int fieldLength, uint specialCount);

    bool AllSpecialConsumed { get; }

    /// <summary>
    /// Finds the index of the next escape character sequence.
    /// </summary>
    /// <param name="value">Field to seek in</param>
    /// <returns></returns>
    int FindNext(ReadOnlySpan<T> value);

    /// <summary>
    /// Ensure that all special characters have been consumed.
    /// </summary>
    void ValidateState(ReadOnlySpan<T> field);
}

internal struct DoubleQuoteUnescaper<T>(T quote, uint quoteCount) : IUnescaper<T> where T : unmanaged, IBinaryInteger<T>
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

        throw new UnreachableException($"Internal error, failed to unescape (token: {typeof(T).FullName}): {error}");
    }
}

internal struct BackslashUnescaper<T>(T escape, uint escapeCount) : IUnescaper<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static int UnescapedLength(int fieldLength, uint specialCount)
    {
        return unchecked((int)((uint)fieldLength - specialCount));
    }

    public bool AllSpecialConsumed => _escapeCount == 0;

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

        throw new UnreachableException($"Internal error, failed to unescape (token: {typeof(T).FullName}): {error}");
    }
}
