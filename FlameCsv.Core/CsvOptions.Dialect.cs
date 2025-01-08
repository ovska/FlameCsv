using System.Runtime.CompilerServices;
using System.Text;
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
        _dialect = new CsvDialect<T>
        {
            Delimiter = T.CreateChecked(_delimiter),
            Quote = T.CreateChecked(_quote),
            Escape = _escape.HasValue ? T.CreateChecked(_escape.Value) : null,
            Newline = GetFromString(_newline),
            Whitespace = GetFromString(_whitespace),
        };

        return ref Nullable.GetValueRefOrDefaultRef(in _dialect);
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
    /// written with preceding or trailing whitespace are quoted.
    /// The default is null/empty.
    /// </summary>
    /// <seealso cref="FieldEscaping"/>
    public string? Whitespace
    {
        get => _whitespace;
        set => this.SetValue(ref _whitespace, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GetNewline(out T newline1, out T newline2, out int newlineLength, bool forWriting = false)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Dialect.Newline.Length, 2, nameof(Newline));

        ReadOnlySpan<T> newline = Dialect.Newline.Span;

        if ((newlineLength = newline.Length) is 0)
        {
            if (forWriting)
                newlineLength = 2;

            newline1 = T.CreateChecked('\r');
            newline2 = T.CreateChecked('\n');
            return;
        }

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
}
