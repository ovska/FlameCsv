using System.Buffers;
using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writers;

[DebuggerDisplay("[CsvTextWriter] Written: {Unflushed} / {_buffer.Length} (inner: {_writer.GetType().Name})")]
internal sealed class CsvTextPipeWriter : ICsvPipeWriter<char>
{
    private readonly TextWriter _writer;
    private readonly ArrayPool<char> _arrayPool;
    private char[] _buffer;

    public CsvTextPipeWriter(TextWriter writer, ArrayPool<char> arrayPool)
    {
        _writer = writer;
        _arrayPool = arrayPool;
        _buffer = arrayPool.Rent(1024);
        Unflushed = 0;
    }

    public int Unflushed { get; private set; }
    public int PreviousLength { get; private set; }

    public Memory<char> GetBuffer()
    {
        var memory = _buffer.AsMemory(Unflushed);
        PreviousLength = memory.Length;
        return memory;
    }

    public void Advance(int length)
    {
        Guard.IsGreaterThanOrEqualTo(length, 0);
        Guard.IsLessThanOrEqualTo(length, _buffer.Length - Unflushed);

        Unflushed += length;
    }

    public async ValueTask<Memory<char>> GrowAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);

        if (PreviousLength >= _buffer.Length)
            ArrayPool<char>.Shared.EnsureCapacity(ref _buffer, PreviousLength * 2);

        var memory = _buffer.AsMemory(Unflushed);
        PreviousLength = memory.Length;
        return memory;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Unflushed > 0)
        {
            await _writer.WriteAsync(_buffer.AsMemory(0, Unflushed), cancellationToken);
            Unflushed = 0;
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
