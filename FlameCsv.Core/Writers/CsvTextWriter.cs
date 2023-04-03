using System.Buffers;
using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writers;

[DebuggerDisplay("[CsvTextWriter] Written: {Unflushed} / {_buffer.Length} (inner: {_writer.GetType().Name})")]
internal sealed class CsvTextWriter : ICsvWriter<char>
{
    private readonly TextWriter _writer;
    private char[] _buffer;
    private int _previousLength;

    public CsvTextWriter(TextWriter writer)
    {
        _writer = writer;
        _buffer = ArrayPool<char>.Shared.Rent(1024);
        Unflushed = 0;
    }

    public Exception? Exception { get; set; }
    public int Unflushed { get; private set; }

    public Span<char> GetBuffer()
    {
        var span = _buffer.AsSpan(Unflushed);
        _previousLength = span.Length;
        return span;
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

        if (_previousLength >= _buffer.Length)
            ArrayPool<char>.Shared.EnsureCapacity(ref _buffer, _previousLength * 2);

        var memory = _buffer.AsMemory(Unflushed);
        _previousLength = memory.Length;
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

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Exception is null)
                await FlushAsync();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(_buffer);
            await _writer.DisposeAsync();
        }
    }
}
