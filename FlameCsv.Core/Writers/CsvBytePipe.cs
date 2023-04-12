using System.Diagnostics;
using System.IO.Pipelines;

namespace FlameCsv.Writers;

[DebuggerDisplay(
    @"\{ CsvPipeWriter, Last Buffer: {_previousLength}, Unflushed: {Unflushed}, Faulted: {Exception != null} \}")]
internal sealed class CsvBytePipe : ICsvPipe<byte>
{
    private readonly PipeWriter _pipeWriter;
    private int _unflushed;

    public CsvBytePipe(PipeWriter pipeWriter)
    {
        _pipeWriter = pipeWriter;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        if (_unflushed > 0)
        {
            await _pipeWriter.FlushAsync(cancellationToken);
            _unflushed = 0;
        }
    }

    public Span<byte> GetSpan()
    {
        return _pipeWriter.GetSpan();
    }

    public Memory<byte> GetMemory()
    {
        return _pipeWriter.GetMemory();
    }

    public void Advance(int length)
    {
        _pipeWriter.Advance(length);
        _unflushed += length;
    }

    public async ValueTask GrowAsync(
        int previousBufferSize,
        CancellationToken cancellationToken = default)
    {
        // flush pending bytes instead of resizing first
        await FlushAsync(cancellationToken);

        if (_pipeWriter.GetSpan().Length < previousBufferSize)
        {
            _ = _pipeWriter.GetSpan(previousBufferSize * 2);
        }
    }

    public ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        return _pipeWriter.CompleteAsync(exception);
    }
}
