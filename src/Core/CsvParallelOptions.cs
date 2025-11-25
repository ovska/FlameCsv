namespace FlameCsv;

/// <summary>
/// Options for parallel CSV processing.
/// </summary>
public readonly record struct CsvParallelOptions
{
    /// <summary>
    /// Size of chunks to use when processing data in parallel.<br/>
    /// When reading CSV, this many records are parsed before they are yielded to the consumer.<br/>
    /// Less critical when writing, the records are batched but the writers are still flushed based on their buffer saturation.<br/>
    /// If unset, defaults to <c>128</c> (<see cref="CsvParallel.DefaultChunkSize"/>)
    /// </summary>
    public int? ChunkSize { get; init; }

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
    public int? MaxDegreeOfParallelism { get; init; }

    internal CsvParallelOptions Validated()
    {
        if (ChunkSize.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ChunkSize.Value, "parallelOptions.ChunkSize");
        }

        if (MaxDegreeOfParallelism.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                MaxDegreeOfParallelism.Value,
                "parallelOptions.MaxDegreeOfParallelism"
            );
        }

        return this;
    }

    internal CsvParallelOptions ValidatedForReading()
    {
        var options = Validated();

        if (!options.MaxDegreeOfParallelism.HasValue)
        {
            options = options with { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4) };
        }

        return options;
    }
}
