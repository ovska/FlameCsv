using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv;

public partial class CsvOptions<T>
{
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

    /// <summary>
    /// Initializes <see cref="_dialect"/>.
    /// </summary>
    /// <remarks>
    /// If overridden, the returned reference must be <see cref="_dialect"/> and must not be null reference.
    /// The dialect must be valid (see <see cref="CsvDialect{T}.Validate"/>).
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual ref readonly CsvDialect<T> InitializeDialect()
    {
        var result = new CsvDialect<T>
        {
            Delimiter = T.CreateChecked(_delimiter),
            Quote = T.CreateChecked(_quote),
            Escape = _escape.HasValue ? T.CreateChecked(_escape.Value) : null,
            Newline = GetSpan(this, _newline, stackalloc T[8]),
            Whitespace = GetSpan(this, _whitespace, stackalloc T[8])
        };

        result.Validate();

        _dialect = result;
        return ref Nullable.GetValueRefOrDefaultRef(in _dialect);

        static ReadOnlySpan<T> GetSpan(CsvOptions<T> @this, string? value, Span<T> buffer)
        {
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
