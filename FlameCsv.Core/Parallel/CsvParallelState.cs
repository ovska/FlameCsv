using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.Parallel;

/// <summary>
/// State passed to invoke instances.
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
    public int RecordIndex { get; init; }

    /// <summary>
    /// The header record in the CSV.
    /// </summary>
    /// <remarks>
    /// If <see cref="CsvOptions{T}.HasHeader"/> is <see langword="false"/>,
    /// the return value will be <see langword="null"/>.
    /// </remarks>
    public CsvHeader? Header { get; init; }
}
