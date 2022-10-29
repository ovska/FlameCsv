using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writers;

internal interface ICsvFormatter<T, in TValue> where T : unmanaged
{
    bool TryFormat(TValue value, Span<T> buffer, out int tokensWritten);
}

internal interface ICsvWriter<T> : IAsyncDisposable where T : unmanaged
{
    public Exception? Exception { get; set; }

    /// <summary>
    /// Amount of unflushed data in the writer.
    /// </summary>
    int Unflushed { get; }

    /// <summary>
    /// Returns a buffer that can be written to.
    /// </summary>
    Span<T> GetBuffer();

    /// <summary>
    /// Signals that the specified amount of tokens have been written to the buffer.
    /// </summary>
    /// <param name="length"></param>
    void Advance(int length);

    /// <summary>
    /// Grows the buffer, either by flushing the data or increasing an unflushed buffer's size.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<Memory<T>> GrowAsync(CancellationToken cancellationToken);
}

internal readonly struct CsvTextWriterWrapper : ICsvWriter<char>
{
    private readonly CsvTextWriter Arg;

    public ValueTask DisposeAsync()
    {
        IFormattable test = null!;
        throw new NotImplementedException();
    }


    public Exception? Exception
    {
        get => Arg.Exception;
        set => Arg.Exception = value;
    }

    public int Unflushed => Arg.Unflushed;
    public Span<char> GetBuffer() => Arg.GetBuffer();
    public void Advance(int length) => Arg.Advance(length);

    public ValueTask<Memory<char>> GrowAsync(CancellationToken cancellationToken)
        => Arg.GrowAsync(cancellationToken);

    public void SetException(Exception ex)
    {
        throw new NotImplementedException();
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => Arg.FlushAsync(cancellationToken);
}

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

        if (_previousLength > _buffer.Length)
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
}

internal sealed class CsvPipeWriter : ICsvWriter<byte>
{
    public Exception? Exception { get; set; }
    public int Unflushed { get; private set; }

    private int _previousLength;
    private readonly PipeWriter _pipeWriter;

    public CsvPipeWriter(PipeWriter pipeWriter)
    {
        _pipeWriter = pipeWriter;
        Unflushed = 0;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        if (Unflushed > 0)
        {
            await _pipeWriter.FlushAsync(cancellationToken);
            Unflushed = 0;
        }
    }

    public Span<byte> GetBuffer()
    {
        var span = _pipeWriter.GetSpan();
        _previousLength = span.Length;
        return span;
    }

    public void Advance(int length)
    {
        _pipeWriter.Advance(length);
        Unflushed += length;
    }

    public async ValueTask<Memory<byte>> GrowAsync(CancellationToken cancellationToken = default)
    {
        // flush pending bytes instead of resizing first
        await FlushAsync(cancellationToken);

        var memory = _pipeWriter.GetMemory();

        // the previous buffer was as big or bigger than the current, we need more space for the formatter
        if (_previousLength >= memory.Length)
        {
            memory = _pipeWriter.GetMemory(_previousLength * 2);
        }

        _previousLength = memory.Length;
        return memory;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Exception is null)
                await _pipeWriter.FlushAsync();
        }
        finally
        {
            await _pipeWriter.CompleteAsync(Exception);
        }
    }
}

internal class CsvStreamWriter
{
    public async ValueTask WriteAsync(PipeWriter pipeWriter)
    {
        await using var writer = new CsvPipeWriter(pipeWriter);
    }
}

internal sealed class WriteState
{
    public static CsvCallback<T, bool> ShouldEscape<T>(Span<T> buffer, CsvParserOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        T[] tokens = new T[2 + options.NewLine.Length + options.Whitespace.Length];

        tokens[0] = options.StringDelimiter;
        tokens[1] = options.Delimiter;
        options.NewLine.CopyTo(tokens.AsMemory(2));
        options.Whitespace.CopyTo(tokens.AsMemory(2 + options.NewLine.Length));

        if (tokens.Length >= 3)
            return Impl;

        T t0 = tokens[0];
        T t1 = tokens[1];
        T t2 = tokens[2];
        return FastImp;

        bool Impl(ReadOnlySpan<T> data, in CsvParserOptions<T> _)
        {
            return data.IndexOfAny(tokens) >= 0;
        }

        bool FastImp(ReadOnlySpan<T> data, in CsvParserOptions<T> _)
        {
            return data.IndexOfAny(t0, t1, t2) >= 0;
        }
    }

    public static void Write<T, TValue, TWriter>(
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        ref TWriter writer)
        where T : unmanaged
        where TWriter : ICsvWriter<T>
    {
        var span = writer.GetBuffer();

        if (formatter.TryFormat(value, span, out int written))
        {
            writer.Advance(written);
            return;
        }
    }
}
