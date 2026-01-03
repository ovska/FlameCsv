using System.Buffers;

namespace FlameCsv.IO.Internal;

internal sealed class ParallelSequenceReader<T> : ParallelReader<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private ReadOnlySequence<T> _data;

    public ParallelSequenceReader(in ReadOnlySequence<T> data, CsvOptions<T> options, in CsvIOOptions ioOptions)
        : base(options, ioOptions)
    {
        _data = data;
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<int>(cancellationToken);

        return new ValueTask<int>(ReadCore(buffer.Span));
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

    protected override ValueTask DisposeAsyncCore()
    {
        _data = default;
        return ValueTask.CompletedTask;
    }

    protected override void DisposeCore()
    {
        _data = default;
    }
}
