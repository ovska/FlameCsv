using System.Buffers;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

internal sealed class StringBuilderPipeWriter : ICsvPipeWriter<char>
{
    private IMemoryOwner<char> _memoryOwner;
    private Memory<char> _memory;
    private readonly MemoryPool<char> _pool;
    private readonly StringBuilder _builder;
    private bool _disposed;

    public StringBuilderPipeWriter(StringBuilder builder, MemoryPool<char> allocator)
    {
        _builder = builder;
        _pool = allocator;
        _memoryOwner = allocator.Rent(4096 / 2);
        _memory = _memoryOwner.Memory;
    }

    public void Advance(int count)
    {
        _builder.Append(_memory.Span.Slice(0, count));
    }

    public Memory<char> GetMemory(int sizeHint = 0)
    {
        if (sizeHint > _memory.Length)
        {
            _pool.EnsureCapacity(ref _memoryOwner, sizeHint);
            _memory = _memoryOwner.Memory;
        }

        return _memory;
    }

    public Span<char> GetSpan(int sizeHint = 0)
    {
        if (sizeHint > _memory.Length)
        {
            _pool.EnsureCapacity(ref _memoryOwner, sizeHint);
            _memory = _memoryOwner.Memory;
        }

        return _memory.Span;
    }

    public bool NeedsFlush => false;

    public void Flush()
    {
    }

    public void Complete(Exception? exception)
    {
        if (_disposed) return;
        _disposed = true;

        _memory = Memory<char>.Empty;
        _memoryOwner.Dispose();
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? new ValueTask(Task.FromCanceled(cancellationToken))
            : default;
    }

    public ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default)
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _memory = Memory<char>.Empty;
        _memoryOwner.Dispose();

        if (exception is not null) return ValueTask.FromException(exception);
        if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);
        return ValueTask.CompletedTask;
    }
}
