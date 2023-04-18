using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Writers;

[DebuggerDisplay("[CsvTextWriter] Written: {Unflushed} / {_buffer.Length} (inner: {_writer.GetType().Name})")]
internal readonly struct CsvCharBufferWriter : IAsyncBufferWriter<char>
{
    private sealed class State
    {
        public int Unflushed;
        public char[] Buffer;

        public State(char[] buffer) => Buffer = buffer;

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
    }

    private readonly TextWriter _writer;
    private readonly ArrayPool<char> _arrayPool;
    private readonly State _state;

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_state.Unflushed / (double)_state.Buffer.Length) >= 0.875;
    }

    public CsvCharBufferWriter(
        TextWriter writer,
        ArrayPool<char>? arrayPool,
        int initialBufferSize = 1024)
    {
        ArgumentNullException.ThrowIfNull(writer);
        Guard.IsGreaterThan(initialBufferSize, 0);

        _writer = writer;
        _arrayPool = arrayPool ?? AllocatingArrayPool<char>.Instance;
        _state = new State(_arrayPool.Rent(initialBufferSize));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> GetSpan(int sizeHint = 0)
    {
        sizeHint = Math.Max(1, sizeHint); // ensure non empty buffer

        if (_state.Remaining < sizeHint)
            EnsureCapacityRare(sizeHint);

        Debug.Assert(_state.Remaining >= sizeHint);
        return _state.Buffer.AsSpan(_state.Unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<char> GetMemory(int sizeHint = 0)
    {
        sizeHint = Math.Max(1, sizeHint); // ensure non empty buffer

        if (_state.Remaining < sizeHint)
            EnsureCapacityRare(sizeHint);

        Debug.Assert(_state.Remaining >= sizeHint);
        return _state.Buffer.AsMemory(_state.Unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        Guard.IsGreaterThanOrEqualTo(length, 0);
        Guard.IsLessThanOrEqualTo(length, _state.Remaining);

        _state.Unflushed += length;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_state.HasUnflushedData)
        {
            await _writer.WriteAsync(_state.Buffer.AsMemory(0, _state.Unflushed), cancellationToken);
            _state.Unflushed = 0;
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            exception ??= new OperationCanceledException(cancellationToken);

        if (exception is null && _state.HasUnflushedData)
        {
            try
            {
                await _writer.WriteAsync(_state.Buffer.AsMemory(0, _state.Unflushed), cancellationToken);
                _state.Unflushed = -1;
            }
            catch (Exception e)
            {
                exception = new CsvWriteException("Exception occured while flushing the writer for the final time.", e);
            }
        }

        _arrayPool.Return(_state.Buffer);
        await _writer.DisposeAsync();

        if (exception is not null)
            throw exception;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureCapacityRare(int sizeHint)
    {
        // check if there is need to copy values
        if (_state.Unflushed == 0)
        {
            _arrayPool.EnsureCapacity(ref _state.Buffer, sizeHint);
        }
        else
        {
            _arrayPool.Resize(ref _state.Buffer, sizeHint + _state.Unflushed);
        }
    }
}
