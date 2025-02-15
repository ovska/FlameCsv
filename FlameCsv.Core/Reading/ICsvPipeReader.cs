using System.ComponentModel;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface ICsvPipeReader<T> : IDisposable, IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Reads the next block of data from the data source.
    /// </summary>
    ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the reader to the specified position.
    /// </summary>
    /// <param name="consumed">End position of the consumed data from the last read.</param>
    /// <param name="examined">End position of the examined data from the last read.</param>
    void AdvanceTo(SequencePosition consumed, SequencePosition examined);
}
