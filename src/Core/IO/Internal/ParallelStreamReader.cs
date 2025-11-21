namespace FlameCsv.IO.Internal;

internal sealed class ParallelStreamReader : ParallelReader<byte>
{
    private readonly Stream _stream;

    public ParallelStreamReader(Stream stream, CsvOptions<byte> options, CsvIOOptions ioOptions)
        : base(options, ioOptions)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    protected override int ReadCore(Span<byte> buffer)
    {
        return _stream.Read(buffer);
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _stream.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        _stream.Dispose();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return _stream.DisposeAsync();
    }
}
