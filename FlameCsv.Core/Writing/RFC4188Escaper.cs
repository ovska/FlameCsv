using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writing;

internal readonly struct RFC4188Escaper<T> : IEscaper<T> where T : unmanaged, IEquatable<T>
{
    public T Quote => _quote;
    public T Escape => _quote;

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly ReadOnlyMemory<T> _newline;

    public RFC4188Escaper(in CsvDialect<T> dialect)
    {
        dialect.DebugValidate();
        Debug.Assert(dialect.IsRFC4188Mode);

        _delimiter = dialect.Delimiter;
        _quote = dialect.Quote;
        _newline = dialect.Newline;
    }

    public bool NeedsEscaping(T value) => value.Equals(_quote);

    public bool NeedsEscaping(ReadOnlySpan<T> value, out int specialCount)
    {
        int index;
        ReadOnlySpan<T> newLine = _newline.Span;

        if (newLine.Length > 1)
        {
            index = value.IndexOfAny(_delimiter, _quote);

            if (index >= 0)
            {
                goto Found;
            }

            specialCount = 0;
            return value.IndexOf(newLine) >= 0;
        }
        else
        {
            // Single token newlines can be seeked directly
            index = value.IndexOfAny(_delimiter, _quote, MemoryMarshal.GetReference(newLine));

            if (index >= 0)
            {
                goto Found;
            }
        }

        Unsafe.SkipInit(out specialCount);
        return false;

        Found:
        specialCount = value.Slice(index).Count(_quote);
        return true;
    }

    public int CountEscapable(ReadOnlySpan<T> value) => value.Count(_quote);
}
