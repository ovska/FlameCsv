using System.Buffers;

namespace FlameCsv.IO;

internal sealed class ConstantSequenceReader<T> : CsvBufferReader<T> where T : unmanaged
{
    private ReadOnlySequence<T> _data;
    private ReadOnlySequence<T> _originalData;

    public ConstantSequenceReader(in ReadOnlySequence<T> data, MemoryPool<T> pool, in CsvReaderOptions options)
        : base(pool, in options)
    {
        _data = data;
        _originalData = data;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _data = _originalData;
        return true;
    }

    protected override int ReadCore(Memory<T> buffer)
    {
        int dataLength = (int)long.Min(int.MaxValue, _data.Length);

        if (dataLength == 0) return 0;

        int length = Math.Min(dataLength, buffer.Length);
        _data.Slice(0, length).CopyTo(buffer.Span);
        _data = _data.Slice(length);
        return length;
    }

    protected override ValueTask<int> ReadAsyncCore(
        Memory<T> buffer,
        CancellationToken cancellationToken)
    {
        return new ValueTask<int>(ReadCore(buffer));
    }

    protected override void DisposeCore()
    {
        _data = default;
        _originalData = default;
    }
}
