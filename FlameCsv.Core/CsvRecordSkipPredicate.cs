namespace FlameCsv;

/// <summary>
/// Arguments for <see cref="CsvRecordSkipPredicate{T}".
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvRecordSkipArgs<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// The current CSV record (unescaped/untrimmed).
    /// </summary>
    public ReadOnlyMemory<T> Record { get; init; }

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options { get; init; }

    /// <summary>
    /// 1-based line number.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Whether a header has been read yet. <see langword="null"/> when reading CSV without a header record.
    /// </summary>
    public bool? HeaderRead { get; init; }
}

/// <summary>
/// Callback for skipping records.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <returns><see langword="true"/> if the record should be skipped</returns>
public delegate bool CsvRecordSkipPredicate<T>(in CsvRecordSkipArgs<T> args) where T : unmanaged, IEquatable<T>;
