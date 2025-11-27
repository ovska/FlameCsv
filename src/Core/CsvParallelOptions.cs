namespace FlameCsv;

/// <summary>
/// Options for parallel CSV processing.
/// </summary>
public readonly record struct CsvParallelOptions
{
    /// <summary>
    /// The default chunk size used when reading or writing in parallel.
    /// </summary>
    public const int DefaultChunkSize = 128;

    private readonly int? _chunkSize;
    private readonly int? _maxDegreeOfParallelism;

    // TODO: ordered processing option

    /// <summary>
    /// Size of chunks to use when processing data in parallel.<br/>
    /// When reading CSV, this many records are parsed before they are yielded to the consumer.<br/>
    /// Less critical when writing, the records are batched but the writers are still flushed based on their buffer saturation.
    /// </summary>
    public int? ChunkSize
    {
        get => _chunkSize;
        init
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Value, nameof(ChunkSize));
            }

            _chunkSize = value;
        }
    }

    /// <summary>
    /// Token to cancel the read or write operation.
    /// </summary>
    /// <remarks>
    /// Cancellation will not guarantee that an <see cref="OperationCanceledException"/> is thrown, only
    /// that the operation will halt as soon as possible.
    /// </remarks>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Maximum degree of parallelism to use when processing CSV data in parallel.<br/>
    /// When reading, the default is <c>4</c> or the number of processors on the machine, whichever is smaller.<br/>
    /// When writing, defaults to the number of processors on the machine, or the one picked by TPL.
    /// </summary>
    public int? MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        init
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Value, nameof(MaxDegreeOfParallelism));
            }

            _maxDegreeOfParallelism = value;
        }
    }

    internal int EffectiveChunkSize => ChunkSize ?? DefaultChunkSize;
    internal int ReadingMaxDegreeOfParallelism => MaxDegreeOfParallelism ?? Math.Min(Environment.ProcessorCount, 4);
}
