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
    private readonly T _newline1;
    private readonly T _newline2;
    private readonly int _newlineLength;
    private readonly ReadOnlyMemory<T> _whitespace;

    public UnixEscaper(CsvOptions<T> options)
    {
        _delimiter = options._delimiter;
        _quote = options._quote;
        _escape = options._escape.GetValueOrDefault();
        _whitespace = options._whitespace;
        options.GetNewline(out _newline1, out _newline2, out _newlineLength);
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

        index = _newlineLength == 1
            ? value.IndexOf(_newline1)
            : value.IndexOf([_newline1, _newline2]);

        if (index >= 0)
        {
            index += _newlineLength;
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
