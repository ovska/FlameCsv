using System.ComponentModel;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail.
/// </summary>
[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface ICsvPipeReader<T> : IDisposable, IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns whether the reader can be used synchronously.
    /// </summary>
    bool SupportsSynchronousReads => true;

    /// <summary>
    /// Reads the next block of data synchronously from the data source.
    /// </summary>
    /// <returns>
    /// A result containing the data, and a boolean indicating whether the reader has reached the end of the data source
    /// and no further data can be read.
    /// </returns>
    CsvReadResult<T> Read();

    /// <summary>
    /// Reads the next block of data asynchronously from the data source.
    /// </summary>
    /// <returns>
    /// A result containing the data, and a boolean indicating whether the reader has reached the end of the data source
    /// and no further data can be read.
    /// </returns>
    ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the reader to the specified position.
    /// </summary>
    /// <param name="consumed">End position of the consumed data from the last read.</param>
    /// <param name="examined">End position of the examined data from the last read.</param>
    void AdvanceTo(SequencePosition consumed, SequencePosition examined);

    /// <summary>
    /// Attempts to reset the reader to the beginning of the data source.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the reader was successfully reset; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryReset();
}
