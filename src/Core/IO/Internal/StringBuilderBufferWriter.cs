using System.Buffers;
using System.Runtime.ExceptionServices;
using System.Text;

namespace FlameCsv.IO.Internal;

internal sealed class StringBuilderBufferWriter : ICsvBufferWriter<char>
{
    private IMemoryOwner<char> _memoryOwner;
    private Memory<char> _memory;
    private readonly MemoryPool<char> _pool;
    private readonly StringBuilder _builder;
    private bool _disposed;

    public StringBuilderBufferWriter(StringBuilder builder, MemoryPool<char> allocator)
    {
        _builder = builder;
        _pool = allocator;
        _memoryOwner = allocator.Rent(4096);
        _memory = _memoryOwner.Memory;
    }

    public void Advance(int count)
    {
        _builder.Append(_memory.Span.Slice(0, count));
    }

    public Memory<char> GetMemory(int sizeHint = 0)
    {
        if (sizeHint > _memory.Length || _memory.IsEmpty)
        {
            _pool.EnsureCapacity(ref _memoryOwner, sizeHint);
            _memory = _memoryOwner.Memory;
        }

        return _memory;
    }

    public Span<char> GetSpan(int sizeHint = 0)
    {
        if (sizeHint > _memory.Length || _memory.IsEmpty)
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

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) : default;
    }

    public ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            exception ??= new OperationCanceledException(cancellationToken);
        }

        try
        {
            Complete(exception);
            return default;
        }
        catch (Exception e)
        {
            return ValueTask.FromException(e);
        }
    }
}
