using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Writing;

[DebuggerDisplay("[CsvCharBufferWriter] Written: {_unflushed} / {Buffer.Length})")]
internal sealed class CsvCharBufferWriter : ICsvBufferWriter<char>
{
    private static readonly int _defaultBufferSize = 4 * Environment.SystemPageSize / sizeof(char);

    private readonly TextWriter _writer;
    private readonly MemoryPool<char> _allocator;
    private readonly int _bufferSize;
    private readonly bool _leaveOpen;
    private int _unflushed;
    private Memory<char> Buffer => _memoryOwner.Memory;
    private IMemoryOwner<char> _memoryOwner;

    public int Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Buffer.Length - _unflushed;
    }

    public bool HasUnflushedData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed > 0;
    }

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_unflushed / (double)Buffer.Length) >= 0.875;
    }

    public CsvCharBufferWriter(TextWriter writer, MemoryPool<char> allocator, int bufferSize, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (bufferSize == -1)
            bufferSize = _defaultBufferSize;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        _writer = writer;
        _allocator = allocator;
        _leaveOpen = leaveOpen;
        _bufferSize = Math.Max(256, bufferSize);
        _memoryOwner = allocator.Rent(_bufferSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> GetSpan(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        if (Remaining < sizeHint || Remaining == 0)
        {
            _allocator.EnsureCapacity(
                ref _memoryOwner,
                minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
                copyOnResize: true);
        }

        Debug.Assert(Remaining >= sizeHint);
        return Buffer.Slice(_unflushed).Span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<char> GetMemory(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        if (Remaining < sizeHint || Remaining == 0)
        {
            _allocator.EnsureCapacity(
                ref _memoryOwner,
                minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
                copyOnResize: true);
        }

        Debug.Assert(Remaining >= sizeHint);
        return Buffer.Slice(_unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        if ((uint)length > (uint)Remaining)
            Throw.Argument_OutOfRange(nameof(length));

        _unflushed += length;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (HasUnflushedData)
        {
            await _writer.WriteAsync(Buffer.Slice(0, _unflushed), cancellationToken).ConfigureAwait(false);
            _unflushed = 0;
        }
    }

    public void Flush()
    {
        if (HasUnflushedData)
        {
            _writer.Write(Buffer.Slice(0, _unflushed).Span);
            _unflushed = 0;
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        try
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
                    exception = new CsvWriteException(
                        "Exception occured while flushing the writer for the final time.",
                        e);
                }
                finally
                {
                    _unflushed = -1;
                }
            }

            if (exception is not null)
                throw exception;
        }
        finally
        {
            using (_memoryOwner)
            {
                if (!_leaveOpen) await _writer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public void Complete(Exception? exception)
    {
        try
        {
            if (exception is null && HasUnflushedData)
            {
                try
                {
                    Flush();
                }
                catch (Exception e)
                {
                    exception = new CsvWriteException(
                        "Exception occured while flushing the writer for the final time.",
                        e);
                }
                finally
                {
                    _unflushed = -1;
                }
            }

            if (exception is not null)
                throw exception;
        }
        finally
        {
            using (_memoryOwner)
            {
                if (!_leaveOpen) _writer.Dispose();
            }
        }
    }
}
