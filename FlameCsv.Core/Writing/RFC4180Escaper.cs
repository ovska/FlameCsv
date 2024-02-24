using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writing;

internal readonly struct RFC4180Escaper<T> : IEscaper<T> where T : unmanaged, IEquatable<T>
{
    public T Quote => _quote;
    public T Escape => _quote;

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly ReadOnlyMemory<T> _newline;
    private readonly ReadOnlyMemory<T> _whitespace;

    public RFC4180Escaper(ref readonly CsvDialect<T> dialect)
    {
        dialect.DebugValidate();
        Debug.Assert(dialect.IsRFC4180Mode);

        _delimiter = dialect.Delimiter;
        _quote = dialect.Quote;
        _newline = dialect.Newline;
        _whitespace = dialect.Whitespace;
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
        ReadOnlySpan<T> newLine = _newline.Span;

        if (newLine.Length != 1)
        {
            index = value.IndexOfAny(_delimiter, _quote);

            if (index >= 0)
            {
                goto FoundQuoteOrDelimiter;
            }

            specialCount = 0;
            return value.IndexOf(newLine) >= 0;
        }
        else
        {
            // Single token newlines can be seeked directly
            index = value.IndexOfAny(_delimiter, _quote, newLine[0]);

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
