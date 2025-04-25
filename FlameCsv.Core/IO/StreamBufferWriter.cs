using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

[DebuggerDisplay("[CsvStreamBufferWriter] Written: {_unflushed} / {_buffer.Length})")]
internal sealed class StreamBufferWriter : CsvBufferWriter<byte>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public StreamBufferWriter(Stream stream, MemoryPool<byte> allocator, in CsvIOOptions options)
        : base(allocator, in options)
    {
        Guard.CanWrite(stream);
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
    }

    protected override void FlushCore(ReadOnlyMemory<byte> memory)
    {
        _stream.Write(memory.Span);
        _stream.Flush();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    protected override async ValueTask FlushAsyncCore(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken)
    {
        await _stream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override void DisposeCore()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    protected override ValueTask DisposeCoreAsync()
    {
        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}
