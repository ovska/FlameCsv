using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Writing;

[DebuggerDisplay("[CsvCharBufferWriter] Written: {_state.Unflushed} / {_state.Buffer.Length})")]
internal readonly struct CsvCharBufferWriter : IDisposable, IAsyncBufferWriter<char>
{
    // this nested class is used to satisfy the struct-constraint for writer in CsvFieldWriter,
    // as a mutable struct doesn't play nice with async methods
    private sealed class State(char[] buffer)
    {
        public int Unflushed;
        public char[] Buffer = buffer;

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
        int initialBufferSize = 4 * 1024)
    {
        ArgumentNullException.ThrowIfNull(writer);
        Guard.IsGreaterThan(initialBufferSize, 0);

        _writer = writer;
        _arrayPool = arrayPool.AllocatingIfNull();
        _state = new State(_arrayPool.Rent(initialBufferSize));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> GetSpan(int sizeHint = 0)
    {
        if (_state.Remaining < sizeHint)
            EnsureCapacityRare(sizeHint);

        Debug.Assert(_state.Remaining >= sizeHint);
        return _state.Buffer.AsSpan(_state.Unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<char> GetMemory(int sizeHint = 0)
    {
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
            await _writer.WriteAsync(_state.Buffer.AsMemory(0, _state.Unflushed), cancellationToken).ConfigureAwait(false);
            _state.Unflushed = 0;
        }
    }

    public void Flush()
    {
        if (_state.HasUnflushedData)
        {
            _writer.Write(_state.Buffer.AsMemory(0, _state.Unflushed));
            _state.Unflushed = 0;
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            exception ??= new OperationCanceledException(cancellationToken);

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

        _arrayPool.Return(_state.Buffer);
        await _writer.DisposeAsync().ConfigureAwait(false);

        if (exception is not null)
            throw exception;
    }

    public void Complete(Exception? exception)
    {
        if (exception is null && _state.HasUnflushedData)
        {
            try
            {
                _writer.WriteAsync(_state.Buffer.AsMemory(0, _state.Unflushed));
                _state.Unflushed = -1;
            }
            catch (Exception e)
            {
                exception = new CsvWriteException("Exception occured while flushing the writer for the final time.", e);
            }
        }

        _arrayPool.Return(_state.Buffer);
        _writer.Dispose();

        if (exception is not null)
            throw exception;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureCapacityRare(int sizeHint)
    {
        Debug.Assert(sizeHint > 0);

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

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
