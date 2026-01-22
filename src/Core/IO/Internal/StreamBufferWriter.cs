using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.IO.Internal;

[DebuggerDisplay("[StreamBufferWriter] Written: {_unflushed} / {_buffer.Length})")]
internal sealed class StreamBufferWriter : CsvBufferWriter<byte>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public StreamBufferWriter(Stream stream, in CsvIOOptions options)
        : base(in options)
    {
        Throw.IfNotWritable(stream);
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
    }

    protected override void DrainCore(ReadOnlyMemory<byte> memory)
    {
        _stream.Write(memory.Span);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    protected override async ValueTask DrainAsyncCore(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken)
    {
        await _stream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
    }

    protected override void FlushCore()
    {
        _stream.Flush();
    }

    protected override ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        return new ValueTask(_stream.FlushAsync(cancellationToken));
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
