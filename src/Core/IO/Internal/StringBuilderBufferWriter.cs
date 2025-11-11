using System.Buffers;
using System.Text;
using FlameCsv.Extensions;

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        _builder.Append(_memory.Span.Slice(0, count));
    }

    public Memory<char> GetMemory(int sizeHint = 0)
    {
        if (sizeHint > _memory.Length || _memory.IsEmpty)
        {
            // copyOnResize is not needed because the data is only copied when advancing,
            // GetMemory is never guaranteed to return the same memory as the previous call
            _pool.EnsureCapacity(ref _memoryOwner, sizeHint, copyOnResize: false);
            _memory = _memoryOwner.Memory;
        }

        return _memory;
    }

    public Span<char> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    // advance already copies
    public bool NeedsFlush => false;

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Complete(Exception? exception)
    {
        DisposeCore();
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Throw.ObjectDisposedAsync(this);

        return cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) : default;
    }

    public ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default)
    {
        try
        {
            DisposeCore();
        }
        catch (Exception e)
        {
            return ValueTask.FromException(e);
        }

        return default;
    }

    private void DisposeCore()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        using (_memoryOwner)
        {
            _memory = Memory<char>.Empty;
            _memoryOwner = HeapMemoryOwner<char>.Empty;
        }
    }
}
