using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;

namespace FlameCsv.Writing;

[DebuggerDisplay(
    @"\{ CsvByteBufferWriter, Unflushed: {Unflushed} \}")]
internal readonly struct CsvByteBufferWriter : ICsvBufferWriter<byte>
{
    public const int InternalFlushThreshold = (int)(4096d * 15d / 16d);

    private readonly PipeWriter _pipeWriter;
    private readonly Box<int> _unflushed = 0; // see explanation in CsvCharBufferWriter

    private ref int Unflushed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _unflushed.GetReference();
    }

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unflushed >= InternalFlushThreshold;
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
        Unflushed += length;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Unflushed > 0)
        {
            await _pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            Unflushed = 0;
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

    private static NotSupportedException NotSupportedEx => new($"{nameof(CsvByteBufferWriter)} does not support synchronous flushing.");
}
