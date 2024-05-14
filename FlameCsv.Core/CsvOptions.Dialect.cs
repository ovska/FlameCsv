using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv;

public partial class CsvOptions<T> : ICsvDialectOptions<T>
{
    internal T _delimiter;
    internal T _quote;
    internal ReadOnlyMemory<T> _newline;
    internal ReadOnlyMemory<T> _whitespace;
    internal T? _escape;

    T ICsvDialectOptions<T>.Delimiter
    {
        get => _delimiter;
        set => this.SetValue(ref _delimiter, value);
    }

    T ICsvDialectOptions<T>.Quote
    {
        get => _quote;
        set => this.SetValue(ref _quote, value);
    }

    ReadOnlyMemory<T> ICsvDialectOptions<T>.Newline
    {
        get => _newline;
        set
        {
            Guard.IsBetween(value.Length, 1, 2, "Newline length");
            this.SetValue(ref _newline, value);
        }
    }

    ReadOnlyMemory<T> ICsvDialectOptions<T>.Whitespace
    {
        get => _whitespace;
        set => this.SetValue(ref _whitespace, value);
    }

    T? ICsvDialectOptions<T>.Escape
    {
        get => _escape;
        set => this.SetValue(ref _escape, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GetNewline(out T newline1, out T newline2, out int newlineLength)
    {
        var span = _newline.Span;
        newline1 = span[0];
        
        if (span.Length == 2)
        {
            newline2 = span[1];
            newlineLength = 2;
        }
        else
        {
            newline2 = default;
            newlineLength = 1;
        }
    }
}
