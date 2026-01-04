namespace FlameCsv.IO.Internal;

#pragma warning disable RCS1229

internal abstract class IOReader<T> : IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    public abstract int Read(Span<T> buffer);
    public abstract void Dispose();

    public virtual ValueTask<int> ReadAsync(Memory<T> buffer, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        try
        {
            int read = Read(buffer.Span);
            return ValueTask.FromResult(read);
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<int>(ex);
        }
    }

    public virtual ValueTask DisposeAsync()
    {
        try
        {
            Dispose();
            return default;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }
}

internal sealed class IOStreamReader : IOReader<byte>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public IOStreamReader(Stream stream, in CsvIOOptions options)
    {
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
    }

    public override int Read(Span<byte> buffer)
    {
        return _stream.Read(buffer);
    }

    public override void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _stream.ReadAsync(buffer, cancellationToken);
    }

    public override ValueTask DisposeAsync()
    {
        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}

internal sealed class IOTextReader : IOReader<char>
{
    private readonly TextReader _reader;
    private readonly bool _leaveOpen;

    public IOTextReader(TextReader reader, in CsvIOOptions options)
    {
        _reader = reader;
        _leaveOpen = options.LeaveOpen;
    }

    public override int Read(Span<char> buffer) => _reader.Read(buffer);

    public override void Dispose()
    {
        if (!_leaveOpen)
        {
            _reader.Dispose();
        }
    }

    public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken) =>
        _reader.ReadAsync(buffer, cancellationToken);
}
