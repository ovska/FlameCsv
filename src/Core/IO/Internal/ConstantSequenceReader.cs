using System.Buffers;

namespace FlameCsv.IO.Internal;

internal sealed class ConstantSequenceReader<T> : CsvBufferReader<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private ReadOnlySequence<T> _data;
    private ReadOnlySequence<T> _originalData;

    public ConstantSequenceReader(in ReadOnlySequence<T> data, in CsvIOOptions options)
        : base(in options)
    {
        _data = data;
        _originalData = data;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _data = _originalData;
        Position = 0;
        return true;
    }

    protected override int ReadCore(Span<T> buffer)
    {
        int dataLength = (int)long.Min(int.MaxValue, _data.Length);

        if (dataLength == 0)
            return 0;

        int length = Math.Min(dataLength, buffer.Length);
        _data.Slice(0, length).CopyTo(buffer);
        _data = _data.Slice(length);
        return length;
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<int>(cancellationToken);

        return new ValueTask<int>(ReadCore(buffer.Span));
    }

    protected override void DisposeCore()
    {
        _data = default;
        _originalData = default;
    }
}
