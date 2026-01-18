using System.Buffers;

// using System.Text;

namespace FlameCsv.IO.Internal;

internal sealed class Utf8SequenceReader : CsvBufferReader<char>
{
    private ReadOnlySequence<byte> _data;
    private ReadOnlySequence<byte> _originalData;

    public Utf8SequenceReader(in ReadOnlySequence<byte> data, in CsvIOOptions options)
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

    protected override int ReadCore(Span<char> buffer)
    {
        throw new NotImplementedException();
        // int written = Encoding.UTF8.GetChars(in _data, buffer);

        // if (written != 0)
        // {
        //     SequencePosition position = _data.GetPosition(written);
        //     _data = _data.Slice(position);
        // }

        // return written;
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<char> buffer, CancellationToken cancellationToken)
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
