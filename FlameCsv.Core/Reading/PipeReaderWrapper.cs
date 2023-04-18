using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

// wrap pipereader to use as ICsvPipeReader<> to avoid duplicate code
internal readonly struct PipeReaderWrapper : ICsvPipeReader<byte>
{
    private readonly PipeReader _inner;

    public PipeReaderWrapper(PipeReader inner) => _inner = inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => _inner.AdvanceTo(consumed, examined);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync() => _inner.CompleteAsync(exception: null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<CsvReadResult<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _inner.ReadAsync(cancellationToken);
        return new CsvReadResult<byte>(result.Buffer, result.IsCompleted);
    }
}
