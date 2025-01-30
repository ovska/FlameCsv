using System.ComponentModel;
using System.IO.Pipelines;

namespace FlameCsv.Reading;

/// <summary>
/// A generic interface mimicking <see cref="System.IO.Pipelines.PipeReader"/>.
/// </summary>
/// <remarks>Internal implementation detail.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICsvPipeReader<T> : IDisposable, IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <seealso cref="PipeReader.ReadAsync(CancellationToken)"/>
    /// <remarks>Internal implementation detail.</remarks>
    ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);

    /// <seealso cref="PipeReader.AdvanceTo(SequencePosition,SequencePosition)"/>
    /// <remarks>Internal implementation detail.</remarks>
    void AdvanceTo(SequencePosition consumed, SequencePosition examined);
}
