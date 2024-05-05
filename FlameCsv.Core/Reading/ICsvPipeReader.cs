namespace FlameCsv.Reading;

internal interface ICsvPipeReader<T> : IDisposable, IAsyncDisposable where T : unmanaged, IEquatable<T>
{
    ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);
    void AdvanceTo(SequencePosition consumed, SequencePosition examined);
}
