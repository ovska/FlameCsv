using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    /// <summary>
    /// Gets or creates the dialect using the configured options.
    /// </summary>
    /// <remarks>
    /// Accessing this property makes the options immutable.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref readonly CsvDialect<T> Dialect
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
        CsvDialect<T> result = new()
        {
            Delimiter = T.CreateChecked(_delimiter),
            Quote = T.CreateChecked(_quote),
            Escape = _escape.HasValue ? T.CreateChecked(_escape.Value) : null,
            Trimming = _trimming,
            Newline = _newline switch
            {
                [var f] => new NewlineBuffer<T>(T.CreateChecked(f)),
                [var f, var s] => new NewlineBuffer<T>(T.CreateChecked(f), T.CreateChecked(s)),
                _ => throw new UnreachableException("Invalid newline: " + _newline),
            },
        };

        result.Validate();
        _dialect = result;
        return ref Nullable.GetValueRefOrDefaultRef(in _dialect);
    }

    private char _delimiter = ',';
    private char _quote = '"';
    private string _newline = "\r\n";
    private char? _escape;
    private CsvFieldTrimming _trimming;

    /// <summary>
    /// The separator character between CSV fields. Default value is <c>,</c>.
    /// </summary>
    public char Delimiter
    {
        get => _delimiter;
        set
        {
            ArgumentOutOfRangeException.ThrowIfZero(value);
            ArgumentOutOfRangeException.ThrowIfEqual(value, ' ');
            this.SetValue(ref _delimiter, value);
        }
    }

    /// <summary>
    /// Characted used to quote strings containing special characters. Default value is <c>"</c>.
    /// </summary>
    public char Quote
    {
        get => _quote;
        set
        {
            ArgumentOutOfRangeException.ThrowIfZero(value);
            ArgumentOutOfRangeException.ThrowIfEqual(value, ' ');
            this.SetValue(ref _quote, value);
        }
    }

    /// <summary>
    /// Optional character used for escaping special characters.
    /// The default value is null, which means RFC4180 escaping (quotes) is used.
    /// </summary>
    public char? Escape
    {
        get => _escape;
        set
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfZero(value.Value, nameof(value));
                ArgumentOutOfRangeException.ThrowIfEqual(value.Value, ' ', nameof(value));
            }

            this.SetValue(ref _escape, value);
        }
    }

    /// <summary>
    /// 1-2 characters long newline string. The default is <c>CRLF</c>.
    /// </summary>
    /// <remarks>
    /// If the newline is 2 characters long, either of the characters is allowed as a record delimiter,
    /// e.g., the default value can read CSV with only <c>LF</c> newlines.
    /// </remarks>
    public string Newline
    {
        get => _newline;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, 2);
            this.SetValue(ref _newline, value);
        }
    }

    /// <summary>
    /// Whether to trim leading and/or trailing spaces from fields.<br/>
    /// The default value is <see cref="CsvFieldTrimming.None"/>.
    /// </summary>
    /// <seealso cref="FieldQuoting"/>
    public CsvFieldTrimming Trimming
    {
        get => _trimming;
        set => this.SetValue(ref _trimming, value);
    }
}
