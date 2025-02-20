using System.Buffers;

namespace FlameCsv.IO;

internal sealed class StreamPipeReader : CsvPipeReader<byte>
{
    private readonly Stream _stream;

    public StreamPipeReader(
        Stream stream,
        MemoryPool<byte> memoryPool,
        in CsvReaderOptions options)
        : base(memoryPool, in options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    protected override int ReadFromInner(Span<byte> buffer)
    {
        return _stream.Read(buffer);
    }

    protected override ValueTask<int> ReadFromInnerAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _stream.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        if (!LeaveOpen)
        {
            _stream.Dispose();
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return LeaveOpen ? default : _stream.DisposeAsync();
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_stream.CanSeek)
        {
            _stream.Seek(0, SeekOrigin.Begin);
            return true;
        }

        return false;
    }
}
