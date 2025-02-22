#if FEATURE_PARALLEL
using System.ComponentModel;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// State containing information about the current parallel reading.
/// </summary>
[PublicAPI]
public readonly struct CsvParallelState<T> where T : unmanaged, IBinaryInteger<T>
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
    /// The options used for reading the CSV.
    /// </summary>
    public CsvOptions<T> Options => Parser.Options;

    /// <summary>
    /// The parser instance.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public required CsvParser<T> Parser
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
#endif
