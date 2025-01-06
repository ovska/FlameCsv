using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    private static void ThrowInvalidTokenType(string? memberName)
    {
        throw new NotSupportedException(
            $"{typeof(CsvOptions<T>).FullName}.{memberName} is not supported by default, inherit the class and override the member.");
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
    private ref readonly CsvDialect<T> InitializeDialect()
    {
        ref readonly CsvDialect<T> result = ref InitializeDialectCore();

#if !DEBUG
        if (GetType() != typeof(CsvOptions<T>))
#endif
        {
            ValidateInitializeDialect(in result);
        }

        return ref result;
    }

    /// <summary>
    /// Initializes <see cref="_dialect"/>.
    /// </summary>
    /// <remarks>
    /// If overridden, the returned reference must be <see cref="_dialect"/> and must not be null reference.
    /// The dialect must be valid (see <see cref="CsvDialect{T}.Validate"/>).
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual ref readonly CsvDialect<T> InitializeDialectCore()
    {
        if (typeof(T) == typeof(char))
        {
            var dialect = new CsvDialect<char>
            {
                Delimiter = _delimiter,
                Escape = _escape,
                Newline = _newline.AsMemory(),
                Quote = _quote,
                Whitespace = _whitespace.AsMemory(),
                NeedsQuoting = null!,
            };
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
                NeedsQuoting = null!,
            };
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

    /// <summary>
    /// The separator character between CSV fields. Default value is <c>,</c>.
    /// </summary>
    public char Delimiter
    {
        get => _delimiter;
        set => this.SetValue(ref _delimiter, value);
    }

    /// <summary>
    /// Characted used to quote strings containing special characters. Default value is <c>"</c>.
    /// </summary>
    public char Quote
    {
        get => _quote;
        set => this.SetValue(ref _quote, value);
    }

    /// <summary>
    /// Optional character used for escaping special characters.
    /// Default value is null, which means RFC4180 escaping (quotes) is used.
    /// </summary>
    public char? Escape
    {
        get => _escape;
        set => this.SetValue(ref _escape, value);
    }

    /// <summary>
    /// 1-2 characters long newline string. If null (the default), newline is automatically detected
    /// between <c>CRLF</c> and <c>LF</c> when reading, and <c>CRLF</c> is used while writing.
    /// </summary>
    public string? Newline
    {
        get => _newline;
        set => this.SetValue(ref _newline, value);
    }

    /// <summary>
    /// Optional whitespace characters that are trimmed out of each field before processing them.
    /// The default is null/empty.
    /// </summary>
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
        ReadOnlySpan<T> newline = Dialect.Newline.Span;

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

            throw new NotSupportedException(
                $"Detecting empty newline is not supported for token type {typeof(T).FullName}");
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
            newline2 = newline1;
            newlineLength = 1;
        }
    }

    private void ValidateInitializeDialect(ref readonly CsvDialect<T> dialect)
    {
        if (Unsafe.IsNullRef(in dialect))
        {
            throw new InvalidOperationException("Overridden dialect init returned a null-ref");
        }

        try
        {
            dialect.Validate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Overridden dialect init returned a non-validated dialect", ex);
        }

        if (!_dialect.HasValue)
        {
            throw new InvalidOperationException("Overridden dialect init did not initialize _dialect propertly");
        }

        if (!Unsafe.AreSame(in dialect, in Nullable.GetValueRefOrDefaultRef(ref _dialect)))
        {
            throw new InvalidOperationException("Returned dialect reference was not the same as _dialect");
        }
    }
}
