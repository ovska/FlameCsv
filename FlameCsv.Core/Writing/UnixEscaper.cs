using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writing;

internal readonly struct UnixEscaper<T> : IEscaper<T> where T : unmanaged, IEquatable<T>
{
    public T Quote => _quote;
    public T Escape => _escape;

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly T _escape;
    private readonly ReadOnlyMemory<T> _newline;
    private readonly ReadOnlyMemory<T> _whitespace;

    public UnixEscaper(in CsvDialect<T> dialect)
    {
        dialect.DebugValidate();
        Debug.Assert(!dialect.IsRFC4188Mode);

        _delimiter = dialect.Delimiter;
        _quote = dialect.Quote;
        _escape = dialect.Escape.Value;
        _newline = dialect.Newline;
        _whitespace = dialect.Whitespace;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsEscaping(T value) => value.Equals(_quote) || value.Equals(_escape);

    public bool NeedsEscaping(ReadOnlySpan<T> value, out int specialCount)
    {
        if (value.IsEmpty)
        {
            specialCount = 0;
            return false;
        }

        int index = value.IndexOfAny(_delimiter, _quote, _escape);

        if (index >= 0)
        {
            goto FoundSpecial;
        }

        ReadOnlySpan<T> newLine = _newline.Span;

        index = value.IndexOf(newLine);

        if (index >= 0)
        {
            index += newLine.Length;
            goto FoundSpecial;
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

        FoundSpecial:
        specialCount = CountEscapable(value.Slice(index));
        return true;
    }

    public int CountEscapable(ReadOnlySpan<T> value)
    {
        int count = 0;

        foreach (var c in value)
        {
            count += NeedsEscaping(c).ToByte();
        }

        return count;
    }
}
