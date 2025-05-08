using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

[DebuggerDisplay("[CsvCharBufferWriter] Written: {_unflushed} / {_buffer.Length})")]
internal sealed class TextBufferWriter : CsvBufferWriter<char>
{
    private readonly TextWriter _writer;
    private readonly bool _leaveOpen;

    public TextBufferWriter(TextWriter writer, MemoryPool<char> allocator, in CsvIOOptions options)
        : base(allocator, in options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _leaveOpen = options.LeaveOpen;
    }

    protected override void FlushCore(ReadOnlyMemory<char> memory)
    {
        _writer.Write(memory.Span);
        _writer.Flush();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    protected override async ValueTask FlushAsyncCore(ReadOnlyMemory<char> memory, CancellationToken cancellationToken)
    {
        await _writer.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override void DisposeCore()
    {
        if (!_leaveOpen)
        {
            _writer.Dispose();
        }
    }

    protected override ValueTask DisposeCoreAsync()
    {
        return _leaveOpen ? default : _writer.DisposeAsync();
    }
}
