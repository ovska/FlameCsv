using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// State containing information about the current parallel reading.
/// </summary>
[PublicAPI]
public readonly struct CsvParallelState
{
    /// <summary>
    /// 1-based index of the current record.
    /// </summary>
    /// <remarks>
    /// If a header record is read, the first yielded record will have an index of 2.
    /// </remarks>
    public int Index { get; init; }

    /// <summary>
    /// The header record in the CSV.
    /// </summary>
    /// <remarks>
    /// If <see cref="CsvOptions{T}.HasHeader"/> is <see langword="false"/>,
    /// the return value will be <see langword="null"/>.
    /// </remarks>
    public CsvHeader? Header { get; init; }

    /// <summary>
    /// When available, contains the state of the internal <see cref="Parallel"/> call.
    /// </summary>
    public ParallelLoopState? LoopState { get; init; }

    /// <summary>
    /// When available, contains the cancellation token for the parallel operation.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
