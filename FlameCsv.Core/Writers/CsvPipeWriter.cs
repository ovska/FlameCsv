using System.Diagnostics;
using System.IO.Pipelines;

namespace FlameCsv.Writers;

[DebuggerDisplay(
    @"\{ CsvPipeWriter, Last Buffer: {_previousLength}, Unflushed: {Unflushed}, Faulted: {Exception != null} \}")]
internal sealed class CsvPipeWriter : ICsvWriter<byte>
{
    public Exception? Exception { get; set; }
    public int Unflushed { get; private set; }

    private int _previousLength;
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

    public Span<byte> GetBuffer()
    {
        var span = _pipeWriter.GetSpan();
        _previousLength = span.Length;
        return span;
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
        if (_previousLength >= memory.Length)
        {
            memory = _pipeWriter.GetMemory(_previousLength * 2);
        }

        _previousLength = memory.Length;
        return memory;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Exception is null)
                await _pipeWriter.FlushAsync();
        }
        finally
        {
            await _pipeWriter.CompleteAsync(Exception);
        }
    }
}
