using System.Buffers;
using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Writers;

[DebuggerDisplay("[CsvTextWriter] Written: {Unflushed} / {_buffer.Length} (inner: {_writer.GetType().Name})")]
internal sealed class CsvTextPipe : ICsvPipe<char>
{
    private readonly TextWriter _writer;
    private readonly ArrayPool<char> _arrayPool;
    private char[] _buffer;
    private int _unflushed;

    public CsvTextPipe(TextWriter writer, ArrayPool<char>? arrayPool)
    {
        _writer = writer;
        _arrayPool = arrayPool ?? AllocatingArrayPool<char>.Instance;
        _buffer = _arrayPool.Rent(1024);
        _unflushed = 0;
    }

    public Span<char> GetSpan()
    {
        return _buffer.AsSpan(_unflushed);
    }

    public Memory<char> GetMemory()
    {
        return _buffer.AsMemory(_unflushed);
    }

    public void Advance(int length)
    {
        Guard.IsGreaterThanOrEqualTo(length, 0);
        Guard.IsLessThanOrEqualTo(length, _buffer.Length - _unflushed);

        _unflushed += length;
    }

    public async ValueTask GrowAsync(
        int previousBufferSize,
        CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);

        if (previousBufferSize >= _buffer.Length)
            ArrayPool<char>.Shared.EnsureCapacity(ref _buffer, previousBufferSize * 2);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed > 0)
        {
            await _writer.WriteAsync(_buffer.AsMemory(0, _unflushed), cancellationToken);
            _unflushed = 0;
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (exception is null)
                await FlushAsync(cancellationToken);
        }
        finally
        {
            _arrayPool.Return(_buffer);
            await _writer.DisposeAsync();
        }
    }
}
