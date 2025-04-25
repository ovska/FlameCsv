using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FlameCsv.Exceptions;

namespace FlameCsv.IO;

[DebuggerDisplay(@"\{ CsvByteBufferWriter, Unflushed: {_unflushed} \}")]
internal sealed class PipeBufferWriter : ICsvBufferWriter<byte>
{
    public static readonly int InternalFlushThreshold = (int)(Environment.SystemPageSize * 15d / 16d);

    private readonly PipeWriter _pipeWriter;
    private int _unflushed;
    private bool _completed;

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed >= InternalFlushThreshold;
    }

    public PipeBufferWriter(PipeWriter pipeWriter)
    {
        ArgumentNullException.ThrowIfNull(pipeWriter);
        _pipeWriter = pipeWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        return _pipeWriter.GetSpan(sizeHint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        return _pipeWriter.GetMemory(sizeHint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        _pipeWriter.Advance(length);
        _unflushed += length;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed > 0)
        {
            _unflushed = 0;

            if (!_completed)
            {
                var result = await _pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                _completed = result.IsCompleted;
            }
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            exception ??= new OperationCanceledException(cancellationToken);

        if (exception is null)
        {
            try
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                exception = CsvWriteException.OnComplete(e);
            }
        }

        await _pipeWriter.CompleteAsync(exception).ConfigureAwait(false);

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    public void Complete(Exception? exception) => _pipeWriter.Complete(exception);
    public void Flush() => throw new NotSupportedException("Synchronous flushing is not supported by PipeWriter.");
}
