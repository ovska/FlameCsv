using System.Diagnostics;

namespace FlameCsv.IO.Internal;

[DebuggerDisplay("[TextBufferWriter] Written: {_unflushed} / {_buffer.Length})")]
internal sealed class TextBufferWriter : CsvBufferWriter<char>
{
    private readonly TextWriter _writer;
    private readonly bool _leaveOpen;

    public TextBufferWriter(TextWriter writer, in CsvIOOptions options)
        : base(in options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _leaveOpen = options.LeaveOpen;
    }

    protected override void DrainCore(ReadOnlyMemory<char> memory)
    {
        _writer.Write(memory.Span);
    }

    protected override ValueTask DrainAsyncCore(ReadOnlyMemory<char> memory, CancellationToken cancellationToken)
    {
        return new ValueTask(_writer.WriteAsync(memory, cancellationToken));
    }

    protected override void FlushCore()
    {
        _writer.Flush();
    }

    protected override ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        return new ValueTask(_writer.FlushAsync(cancellationToken));
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
