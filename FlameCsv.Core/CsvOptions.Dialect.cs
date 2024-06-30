using System.Diagnostics;
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
            Guard.IsBetween(value.Length, 0, 2, "Newline length");
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

    internal ReadOnlySpan<T> GetNewlineSpan(Span<T> buffer)
    {
        if (_newline.Length != 0)
            return _newline.Span;

        GetNewline(out T newline1, out T newline2, out int newlineLength, forWriting: true);

        buffer[0] = newline1;
        buffer[1] = newline2;
        return buffer.Slice(0, newlineLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GetNewline(out T newline1, out T newline2, out int newlineLength, bool forWriting = false)
    {
        if ((newlineLength = _newline.Length) is 0)
        {
            if (forWriting)
                newlineLength = 2;

            if (typeof(T) == typeof(char))
            {
                char cr = '\r';
                char lf = '\n';

                newline1 = Unsafe.As<char, T>(ref cr);
                newline2 = Unsafe.As<char, T>(ref lf);
                return;
            }

            if (typeof(T) == typeof(byte))
            {
                byte cr = (byte)'\r';
                byte lf = (byte)'\n';

                newline1 = Unsafe.As<byte, T>(ref cr);
                newline2 = Unsafe.As<byte, T>(ref lf);
                return;
            }

            throw new NotSupportedException($"Detecting empty newline is not supported for token type {typeof(T).FullName}");
        }

        Debug.Assert(newlineLength is 1 or 2);

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
