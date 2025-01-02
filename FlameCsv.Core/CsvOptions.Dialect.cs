using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    private static void ThrowInvalidTokenType(string? memberName)
    {
        throw new NotSupportedException(
            $"{typeof(CsvOptions<T>).ToTypeString()}.{memberName} is not supported by default, inherit the class and override the member.");
    }

    internal ref readonly CsvDialect<T> Dialect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_dialect.HasValue)
            {
                return ref Nullable.GetValueRefOrDefaultRef(in _dialect);
            }

            return ref InitializeDialect();
        }
    }

    private CsvDialect<T>? _dialect;

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual ref readonly CsvDialect<T> InitializeDialect()
    {
        if (typeof(T) == typeof(char))
        {
            var dialect = new CsvDialect<char>
            {
                Delimiter = _delimiter,
                Escape = _escape,
                Newline = _newline.AsMemory(),
                Quote = _quote,
                Whitespace = _whitespace.AsMemory()
            };
            dialect.Validate();
            _dialect = Unsafe.As<CsvDialect<char>, CsvDialect<T>>(ref dialect);
            return ref Nullable.GetValueRefOrDefaultRef(in _dialect);
        }

        if (typeof(T) == typeof(byte))
        {
            var dialect = new CsvDialect<byte>
            {
                Delimiter = (Utf8Char)_delimiter,
                Quote = (Utf8Char)_quote,
                Escape = (Utf8Char?)_escape,
                Newline = (Utf8String)_newline,
                Whitespace = (Utf8String)_whitespace,
            };
            dialect.Validate();
            _dialect = Unsafe.As<CsvDialect<byte>, CsvDialect<T>>(ref dialect);
            return ref Nullable.GetValueRefOrDefaultRef(in _dialect);
        }

        ThrowInvalidTokenType(nameof(InitializeDialect));
        return ref Unsafe.NullRef<CsvDialect<T>>();
    }

    private char _delimiter = ',';
    private char _quote = '"';
    private string? _newline;
    private string? _whitespace;
    private char? _escape;

    public char Delimiter
    {
        get => _delimiter;
        set => this.SetValue(ref _delimiter, value);
    }

    public char Quote
    {
        get => _quote;
        set => this.SetValue(ref _quote, value);
    }

    public char? Escape
    {
        get => _escape;
        set => this.SetValue(ref _escape, value);
    }

    public string? Newline
    {
        get => _newline;
        set => this.SetValue(ref _newline, value);
    }

    public string? Whitespace
    {
        get => _whitespace;
        set => this.SetValue(ref _whitespace, value);
    }

    internal ReadOnlySpan<T> GetNewlineSpan(Span<T> buffer)
    {
        ref readonly CsvDialect<T> dialect = ref Dialect;
        ReadOnlySpan<T> newline = dialect.Newline.Span;

        if (newline.Length != 0)
            return newline;

        GetNewline(out T newline1, out T newline2, out int newlineLength, forWriting: true);

        buffer[0] = newline1;
        buffer[1] = newline2;
        return buffer.Slice(0, newlineLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GetNewline(out T newline1, out T newline2, out int newlineLength, bool forWriting = false)
    {
        ref readonly CsvDialect<T> dialect = ref Dialect;
        ReadOnlySpan<T> newline = dialect.Newline.Span;

        if ((newlineLength = newline.Length) is 0)
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

            throw new NotSupportedException($"Detecting empty newline is not supported for token type {typeof(T).ToTypeString()}");
        }

        Debug.Assert(newlineLength is 1 or 2);

        newline1 = newline[0];

        if (newline.Length == 2)
        {
            newline2 = newline[1];
            newlineLength = 2;
        }
        else
        {
            newline2 = default;
            newlineLength = 1;
        }
    }
}
