using System.Buffers;

namespace FlameCsv.Reading.Internal;

internal sealed class TextPipeReader : CsvPipeReader<char>
{
    private readonly TextReader _reader;

    public TextPipeReader(
        TextReader reader,
        MemoryPool<char> allocator,
        in CsvReaderOptions options)
        : base(allocator, in options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    protected override int ReadFromInner(Span<char> buffer)
    {
        return _reader.Read(buffer);
    }

    protected override ValueTask<int> ReadFromInnerAsync(Memory<char> buffer, CancellationToken cancellationToken)
    {
        return _reader.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        if (!_leaveOpen)
        {
            _reader.Dispose();
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        DisposeCore();
        return default;
    }
}
