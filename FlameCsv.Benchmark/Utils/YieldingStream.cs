using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Benchmark.Utils;

[SuppressMessage("Performance", "CA1835:Prefer the \'Memory\'-based overloads for \'ReadAsync\' and \'WriteAsync\'")]
internal sealed class YieldingStream(
    Stream inner,
    TimeSpan ioDelay = default) : Stream
{
    public override void Flush()
    {
        inner.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return inner.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
    }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await Task.Delay(ioDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return await inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
    {
        await Task.Delay(ioDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return await inner.ReadAsync(buffer, cancellationToken);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await Task.Delay(ioDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        await inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
    {
        await Task.Delay(ioDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        await inner.WriteAsync(buffer, cancellationToken);
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(ioDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        await inner.FlushAsync(cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await Task.Delay(ioDelay).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        await inner.DisposeAsync();
    }
}
