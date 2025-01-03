using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Writing;

[DebuggerDisplay("[CsvCharBufferWriter] Written: {_state.Unflushed} / {_state.Buffer.Length})")]
internal readonly struct CsvCharBufferWriter : ICsvBufferWriter<char>
{
    // this nested class is used to satisfy the struct-constraint for writer in CsvFieldWriter,
    // as a mutable struct doesn't play nice with async methods
    private sealed class State : IDisposable
    {
        public int Unflushed;
        public Memory<char> Buffer => Owner.Memory;
        public IMemoryOwner<char> Owner;
        public readonly MemoryPool<char> Allocator;

        public State(MemoryPool<char> allocator, int initialLength)
        {
            Allocator = allocator;
            Owner = Allocator.Rent(initialLength);
        }

        public int Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Buffer.Length - Unflushed;
        }

        public bool HasUnflushedData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unflushed > 0;
        }

        public void Dispose()
        {
            Owner.Dispose();
            Owner = null!;
        }
    }

    private readonly TextWriter _writer;
    private readonly State _state;

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_state.Unflushed / (double)_state.Buffer.Length) >= 0.875;
    }

    public CsvCharBufferWriter(
        TextWriter writer,
        MemoryPool<char> allocator,
        int initialBufferSize = 4 * 1024)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialBufferSize);

        _writer = writer;
        _state = new State(allocator, initialBufferSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> GetSpan(int sizeHint = 0)
    {
        if (_state.Remaining < sizeHint)
        {
            _state.Allocator.EnsureCapacity(ref _state.Owner, sizeHint + _state.Unflushed, copyOnResize: true);
        }

        Debug.Assert(_state.Remaining >= sizeHint);
        return _state.Buffer.Span.Slice(_state.Unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<char> GetMemory(int sizeHint = 0)
    {
        if (_state.Remaining < sizeHint)
        {
            _state.Allocator.EnsureCapacity(ref _state.Owner, sizeHint + _state.Unflushed, copyOnResize: true);
        }

        Debug.Assert(_state.Remaining >= sizeHint);
        return _state.Buffer.Slice(_state.Unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, _state.Remaining);

        _state.Unflushed += length;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_state.HasUnflushedData)
        {
            await _writer.WriteAsync(_state.Buffer.Slice(0, _state.Unflushed), cancellationToken).ConfigureAwait(false);
            _state.Unflushed = 0;
        }
    }

    public void Flush()
    {
        if (_state.HasUnflushedData)
        {
            _writer.Write(_state.Buffer.Span.Slice(0, _state.Unflushed));
            _state.Unflushed = 0;
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            exception ??= new OperationCanceledException(cancellationToken);

        await using (_writer.ConfigureAwait(false))
        using (_state)
        {
            if (exception is null)
            {
                try
                {
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    exception = new CsvWriteException("Exception occured while flushing the writer for the final time.", e);
                }
            }
        }

        if (exception is not null)
            throw exception;
    }

    public void Complete(Exception? exception)
    {
        using (_state)
        using (_writer)
        {
            if (exception is null && _state.HasUnflushedData)
            {
                try
                {
                    _writer.WriteAsync(_state.Buffer.Slice(0, _state.Unflushed));
                    _state.Unflushed = -1;
                }
                catch (Exception e)
                {
                    exception = new CsvWriteException("Exception occured while flushing the writer for the final time.", e);
                }
            }
        }

        if (exception is not null)
            throw exception;
    }
}
