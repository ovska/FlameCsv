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
    private readonly int? _maxQueuedChunks;
    private readonly int? _maxDegreeOfParallelism;

    /// <summary>
    /// Whether order does not need to be preserved.
    /// Set this to <c>true</c> to improve performance when exact record order is not required.<br/>
    /// The default is <c>false</c>, which will preserve record order at the cost of more waiting.
    /// </summary>
    public bool Unordered { get; init; }

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
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// The number of chunks that can be in-flight at any given time (see <see cref="ChunkSize"/>).
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    /// <remarks>
    /// This value controls capacity of the bounded <see cref="System.Threading.Channels.Channel{T}"/> used internally.
    /// </remarks>
    public int? MaxQueuedChunks
    {
        get => _maxQueuedChunks;
        init
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Value, nameof(MaxQueuedChunks));
            }

            _maxQueuedChunks = value;
        }
    }

    /// <summary>
    /// Maximum degree of parallelism to use. This value (or <c>-1</c> for if <c>null</c>) is passed to TPL methods.
    /// </summary>
    /// <remarks>
    /// This value controls how many producers (<see cref="Reading.IMaterializer{T, TResult}"/> or <see cref="Writing.IDematerializer{T, TValue}"/>)
    /// can be active at any given time.
    /// </remarks>
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

    /// <summary>
    /// Implicitly creates a <see cref="CsvParallelOptions"/> from a <see cref="CancellationToken"/>,
    /// setting the <see cref="CancellationToken"/> property.
    /// </summary>
    public static implicit operator CsvParallelOptions(CancellationToken cancellationToken) =>
        new() { CancellationToken = cancellationToken };

    /// <summary>
    /// Explicitly converts to <see cref="ParallelOptions"/> for use with TPL methods.
    /// </summary>
    public static explicit operator ParallelOptions(in CsvParallelOptions options) =>
        new()
        {
            CancellationToken = options.CancellationToken,
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism ?? -1,
        };

    /// <summary>
    /// Creates a <see cref="CsvParallelOptions"/> from a <see cref="ParallelOptions"/>,
    /// </summary>
    /// <remarks>
    /// Sets the <see cref="CancellationToken"/> and <see cref="MaxDegreeOfParallelism"/> properties.
    /// </remarks>
    public static explicit operator CsvParallelOptions(ParallelOptions options) =>
        new()
        {
            CancellationToken = options.CancellationToken,
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism == -1 ? null : options.MaxDegreeOfParallelism,
        };
}
