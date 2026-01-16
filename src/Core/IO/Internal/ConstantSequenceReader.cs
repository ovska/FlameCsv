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
        Check.False(data.IsSingleSegment, "Data must be multi-segment.");
        _data = data;
        _originalData = data;
    }

    protected override bool TryResetCore()
    {
        _data = _originalData;
        return true;
    }

    protected override int ReadCore(Span<T> buffer)
    {
        int length = (int)long.Min(_data.Length, buffer.Length);

        if (length != 0)
        {
            SequencePosition position = _data.GetPosition(length);
            _data.Slice(0, position).CopyTo(buffer);
            _data = _data.Slice(position);
        }

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
