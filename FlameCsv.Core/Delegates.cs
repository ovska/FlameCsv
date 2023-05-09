namespace FlameCsv;

public readonly struct CsvRowSkipArgs<T> where T : unmanaged, IEquatable<T>
{
    public ReadOnlyMemory<T> Record { get; init; }
    public CsvDialect<T> Dialect { get; init; }
    public int Line { get; init; }
}

public delegate bool RowSkipCallback<T>(CsvRowSkipArgs<T> args)
    where T : unmanaged, IEquatable<T>;

public readonly struct CsvExceptionHandlerArgs<T> where T : unmanaged, IEquatable<T>
{
    public ReadOnlyMemory<T> Record { get; init; }
    public CsvDialect<T> Dialect { get; init; }
    public int Line { get; init; }
    public Exception Exception { get; init; }
}

/// <summary>
/// Callback for a CSV record or a single field.
/// </summary>
/// <param name="data">The tokens, may be a trimmed field or a whole row</param>
/// <param name="tokens">Current operation's CSV tokens</param>
/// <typeparam name="T">CSV token type</typeparam>
/// <typeparam name="TReturn">Return value</typeparam>
public delegate TReturn CsvCallback<T, out TReturn>(
    ReadOnlyMemory<T> data,
    in CsvDialect<T> tokens)
    where T : unmanaged, IEquatable<T>;

/// <summary>
/// Callback for custom handling of parsing errors.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <returns><see langword="true"/> if the exception can be ignored.</returns>
public delegate bool CsvExceptionHandler<T>(CsvExceptionHandlerArgs<T> args)
    where T : unmanaged, IEquatable<T>;

/// <summary>
/// Callback that returns a boolean value for a given span.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
internal delegate bool SpanPredicate<T>(ReadOnlySpan<T> data);

/// <summary>
/// Callback that returns a boolean value for a given span.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
internal delegate bool SpanPredicate<T, in TArg>(ReadOnlySpan<T> data, TArg arg);
