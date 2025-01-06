using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv.Writing;

[DebuggerDisplay(@"\{ CsvByteBufferWriter, Unflushed: {_unflushed} \}")]
internal sealed class CsvByteBufferWriter : ICsvBufferWriter<byte>
{
    public static readonly int InternalFlushThreshold = (int)(Environment.SystemPageSize * 15d / 16d);

    private readonly PipeWriter _pipeWriter;
    private int _unflushed; // see explanation in CsvCharBufferWriter

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed >= InternalFlushThreshold;
    }

    public CsvByteBufferWriter(PipeWriter pipeWriter)
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

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed > 0)
        {
            await _pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            _unflushed = 0;
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
                exception = new CsvWriteException("Exception occured while flushing the writer for the final time.", e);
            }
        }

        await _pipeWriter.CompleteAsync(exception).ConfigureAwait(false);

        if (exception is not null)
            throw exception;
    }

    public void Complete(Exception? exception) => throw NotSupportedEx;
    public void Flush() => throw NotSupportedEx;

    private static NotSupportedException NotSupportedEx
        => new($"{nameof(CsvByteBufferWriter)} does not support synchronous flushing.");
}
