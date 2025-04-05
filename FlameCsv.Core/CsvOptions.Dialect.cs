using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    /// <summary>
    /// Gets or creates the dialect using the configured options.
    /// </summary>
    protected internal ref readonly CsvDialect<T> Dialect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_dialect.HasValue)
            {
                return ref Nullable.GetValueRefOrDefaultRef(in _dialect);
            }

            return ref InitializeDialectCore();
        }
    }

    private CsvDialect<T>? _dialect;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ref readonly CsvDialect<T> InitializeDialectCore()
    {
        CsvDialect<T> result = InitializeDialect();
        result.Validate();
        _dialect = result;
        return ref Nullable.GetValueRefOrDefaultRef(in _dialect);
    }

    /// <summary>
    /// Initializes the dialect.
    /// </summary>
    /// <remarks>
    /// If overridden, the dialect must be valid (see <see cref="CsvDialect{T}.Validate"/>).
    /// </remarks>
    protected virtual CsvDialect<T> InitializeDialect()
    {
        return new CsvDialect<T>
        {
            Delimiter = T.CreateChecked(_delimiter),
            Quote = T.CreateChecked(_quote),
            Escape = _escape.HasValue ? T.CreateChecked(_escape.Value) : null,
            Whitespace = string.IsNullOrEmpty(_whitespace) ? [] : GetSpan(this, _whitespace, stackalloc T[8]),
            Newline = _newline switch
            {
                null or "" => default,
                [T f] => new NewlineBuffer<T>(f),
                [T f, T s] => new NewlineBuffer<T>(f, s),
                _ => throw new CsvConfigurationException("Newline must be 1 or 2 characters long."),
            },
        };

        static ReadOnlySpan<T> GetSpan(CsvOptions<T> @this, string value, Span<T> buffer)
        {
            if (typeof(T) == typeof(char))
            {
                return value.AsSpan().Cast<char, T>();
            }

            if (typeof(T) == typeof(byte))
            {
                if (value == Utf8String.CRLF) return Utf8String.CRLF.Span.Cast<byte, T>();
                if (value == Utf8String.LF) return Utf8String.LF.Span.Cast<byte, T>();
                if (value == Utf8String.Space) return Utf8String.Space.Span.Cast<byte, T>();
                return Encoding.UTF8.GetBytes(value).AsSpan().Cast<byte, T>();
            }

            return @this.TryWriteChars(value, buffer, out int written) && (uint)written <= (uint)buffer.Length
                ? buffer.Slice(0, written)
                : @this.GetFromString(value).Span;
        }
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
    /// The default value is null, which means RFC4180 escaping (quotes) is used.
    /// </summary>
    public char? Escape
    {
        get => _escape;
        set => this.SetValue(ref _escape, value);
    }

    /// <summary>
    /// 1-2 characters long newline string. If null or empty (the default), newline is automatically detected
    /// between <c>CRLF</c> and <c>LF</c> when reading and <c>CRLF</c> is used while writing.
    /// </summary>
    public string? Newline
    {
        get => _newline;
        set => this.SetValue(ref _newline, value);
    }

    /// <summary>
    /// Optional whitespace characters that are trimmed out of each field before processing them, and fields
    /// written with the preceding or trailing whitespace are quoted.
    /// The default is null/empty.
    /// </summary>
    /// <seealso cref="FieldQuoting"/>
    public string? Whitespace
    {
        get => _whitespace;
        set => this.SetValue(ref _whitespace, value);
    }
}
