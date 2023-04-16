using System.Diagnostics;
using System.IO.Pipelines;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;

namespace FlameCsv.Writers;

[DebuggerDisplay(
    @"\{ CsvPipeWriter, Unflushed: {Unflushed} \}")]
internal readonly struct CsvByteBufferWriter : IAsyncBufferWriter<byte>
{
    private readonly PipeWriter _pipeWriter;
    private readonly Box<int> _unflushed = 0;

    public CsvByteBufferWriter(PipeWriter pipeWriter)
    {
        ArgumentNullException.ThrowIfNull(pipeWriter);
        _pipeWriter = pipeWriter;
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        return _pipeWriter.GetSpan(sizeHint);
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        return _pipeWriter.GetMemory(sizeHint);
    }

    public void Advance(int length)
    {
        _pipeWriter.Advance(length);
        _unflushed.GetReference() += length;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed > 0)
        {
            await _pipeWriter.FlushAsync(cancellationToken);
            _unflushed.GetReference() = 0;
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            exception ??= new OperationCanceledException(cancellationToken);

        if (exception is null && _unflushed > 0)
        {
            try
            {
                await _pipeWriter.FlushAsync(cancellationToken);
                _unflushed.GetReference() = -1;
            }
            catch (Exception e)
            {
                exception = new CsvWriteException("Exception occured while flushing the writer for the final time.", e);
            }
        }

        await _pipeWriter.CompleteAsync(exception);

        if (exception is not null)
            throw exception;
    }
}
