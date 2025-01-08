namespace FlameCsv;

/// <summary>
/// Callback for custom handling of parsing errors.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <returns><see langword="true"/> if the exception can be ignored.</returns>
public delegate bool CsvExceptionHandler<T>(CsvExceptionHandlerArgs<T> args) where T : unmanaged, IBinaryInteger<T>;

/// <summary>
/// Arguments for <see cref="CsvExceptionHandler{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly ref struct CsvExceptionHandlerArgs<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// The CSV record that caused the exception (unescaped/untrimmed).
    /// </summary>
    public ReadOnlySpan<T> Record { get; init; }

    /// <summary>
    /// Current options.
    /// </summary>
    public CsvOptions<T> Options { get; init; }

    /// <summary>
    /// 1-based line number.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Exception thrown.
    /// </summary>
    public Exception Exception { get; init; }
}
