namespace FlameCsv.IO.Internal;

internal sealed class ParallelTextReader : ParallelReader<char>
{
    private readonly TextReader _reader;

    public ParallelTextReader(TextReader reader, CsvOptions<char> options, CsvIOOptions ioOptions)
        : base(options, ioOptions)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    protected override int ReadCore(Span<char> buffer)
    {
        return _reader.Read(buffer);
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<char> buffer, CancellationToken cancellationToken)
    {
        return _reader.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        _reader.Dispose();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _reader.Dispose();
        return default;
    }
}
