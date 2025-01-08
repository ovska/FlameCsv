namespace FlameCsv;

/// <summary>
/// Arguments for <see cref="CsvRecordSkipPredicate{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly ref struct CsvRecordSkipArgs<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// The current CSV record (unescaped/untrimmed).
    /// </summary>
    public ReadOnlySpan<T> Record { get; init; }

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options { get; init; }

    /// <summary>
    /// 1-based line number.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Whether this is the header record.
    /// </summary>
    /// <remarks>
    /// This is generally true only for the very first line when reading CSV with leader,
    /// and always false when the CSV does not have a header.
    /// </remarks>
    public bool IsHeader { get; init; }
}

/// <summary>
/// Callback for skipping records.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <returns><see langword="true"/> if the record should be skipped</returns>
public delegate bool CsvRecordSkipPredicate<T>(CsvRecordSkipArgs<T> args) where T : unmanaged, IBinaryInteger<T>;
