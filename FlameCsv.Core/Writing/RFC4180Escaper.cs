using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writing;

internal readonly struct RFC4180Escaper<T> : IEscaper<T> where T : unmanaged, IEquatable<T>
{
    public T Quote => _quote;
    public T Escape => _quote;

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly T _newline1;
    private readonly T _newline2;
    private readonly int _newlineLength;
    private readonly ReadOnlyMemory<T> _whitespace;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RFC4180Escaper(CsvOptions<T> options)
    {
        Debug.Assert(!options._escape.HasValue);
        _delimiter = options._delimiter;
        _quote = options._quote;
        _whitespace = options._whitespace;
        options.GetNewline(out _newline1, out _newline2, out _newlineLength, forWriting: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsEscaping(T value) => value.Equals(_quote);

    public bool NeedsEscaping(ReadOnlySpan<T> value, out int specialCount)
    {
        if (value.IsEmpty)
        {
            specialCount = 0;
            return false;
        }

        int index;

        if (_newlineLength != 1)
        {
            index = value.IndexOfAny(_delimiter, _quote);

            if (index >= 0)
            {
                goto FoundQuoteOrDelimiter;
            }

            specialCount = 0;
            return value.IndexOf([_newline1, _newline2]) >= 0;
        }
        else
        {
            // Single token newlines can be seeked directly
            index = value.IndexOfAny(_delimiter, _quote, _newline1);

            if (index >= 0)
            {
                goto FoundQuoteOrDelimiter;
            }
        }

        specialCount = 0;

        if (!_whitespace.IsEmpty)
        {
            ref T first = ref value.DangerousGetReference();
            ref T last = ref Unsafe.Add(ref first, value.Length - 1);

            foreach (T token in _whitespace.Span)
            {
                if (first.Equals(token) || last.Equals(token))
                {
                    return true;
                }
            }
        }

        return false;

        FoundQuoteOrDelimiter:
        specialCount = CountEscapable(value.Slice(index));
        return true;
    }

    public int CountEscapable(ReadOnlySpan<T> value) => System.MemoryExtensions.Count(value, _quote);
}
