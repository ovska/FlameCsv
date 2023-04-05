using System.Diagnostics;
using System.IO.Pipelines;

namespace FlameCsv.Writers;

[DebuggerDisplay(
    @"\{ CsvPipeWriter, Last Buffer: {_previousLength}, Unflushed: {Unflushed}, Faulted: {Exception != null} \}")]
internal sealed class CsvPipeWriter : ICsvPipeWriter<byte>
{
    public int Unflushed { get; private set; }
    public int PreviousLength { get; private set; }

    private readonly PipeWriter _pipeWriter;

    public CsvPipeWriter(PipeWriter pipeWriter)
    {
        _pipeWriter = pipeWriter;
        Unflushed = 0;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        if (Unflushed > 0)
        {
            await _pipeWriter.FlushAsync(cancellationToken);
            Unflushed = 0;
        }
    }

    public Memory<byte> GetBuffer()
    {
        var memory = _pipeWriter.GetMemory();
        PreviousLength = memory.Length;
        return memory;
    }

    public void Advance(int length)
    {
        _pipeWriter.Advance(length);
        Unflushed += length;
    }

    public async ValueTask<Memory<byte>> GrowAsync(CancellationToken cancellationToken = default)
    {
        // flush pending bytes instead of resizing first
        await FlushAsync(cancellationToken);

        var memory = _pipeWriter.GetMemory();

        // the previous buffer was as big or bigger than the current, we need more space for the formatter
        if (PreviousLength >= memory.Length)
        {
            memory = _pipeWriter.GetMemory(PreviousLength * 2);
        }

        PreviousLength = memory.Length;
        return memory;
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (exception is null)
                await _pipeWriter.FlushAsync(cancellationToken);
        }
        finally
        {
            await _pipeWriter.CompleteAsync(exception);
        }
    }
}
