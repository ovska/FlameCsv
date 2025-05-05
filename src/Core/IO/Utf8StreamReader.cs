using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Unicode;

namespace FlameCsv.IO;

internal sealed class Utf8StreamReader : CsvBufferReader<char>
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private int _count;
    private int _offset;
    private bool _endOfStream;
    private bool _preambleRead;

    public Utf8StreamReader(Stream stream, MemoryPool<char>? memoryPool, in CsvIOOptions options)
        : base(memoryPool ?? MemoryPool<char>.Shared, in options)
    {
        _stream = stream;
        _buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
    }

    protected override int ReadCore(Memory<char> buffer)
    {
        int totalCharsWritten = 0;
        Span<char> bufferSpan = buffer.Span;

        while (true)
        {
            if (_offset >= _count && !_endOfStream)
            {
                _count = _stream.Read(_buffer, 0, _buffer.Length);
                _offset = 0;

                if (_count == 0)
                {
                    _endOfStream = true;
                    break;
                }
            }

            ReadOnlySpan<byte> byteSpan = new(_buffer, _offset, _count - _offset);
            Span<char> charSpan = bufferSpan.Slice(totalCharsWritten);

            if (!_preambleRead)
            {
                ReadPreamble(ref byteSpan);
            }

            OperationStatus status = Utf8.ToUtf16(
                byteSpan,
                charSpan,
                out int bytesConsumed,
                out int charsWritten,
                replaceInvalidSequences: true,
                isFinalBlock: _endOfStream
            );

            _offset += bytesConsumed;
            totalCharsWritten += charsWritten;

            if (status is OperationStatus.Done or OperationStatus.DestinationTooSmall)
            {
                break;
            }

            if (status == OperationStatus.NeedMoreData)
            {
                // Shift any leftover bytes to front
                int remaining = _count - _offset;
                if (remaining > 0)
                {
                    _buffer.AsSpan(_offset, remaining).CopyTo(_buffer);
                    _count = remaining;
                }
                else
                {
                    _count = 0;
                }

                _offset = 0;

                if (_endOfStream)
                    break;

                int bytesRead = _stream.Read(_buffer, _count, _buffer.Length - _count);

                if (bytesRead == 0)
                {
                    _endOfStream = true;
                    break;
                }

                _count += bytesRead;
            }
            else if (status == OperationStatus.InvalidData)
            {
                // Replace an invalid byte sequence with U+FFFD
                if (totalCharsWritten < buffer.Length)
                {
                    bufferSpan[totalCharsWritten++] = '\uFFFD';
                }
                else
                {
                    // User buffer full
                    break;
                }

                // Skip one byte to move past invalid data
                _offset += Math.Max(bytesConsumed, 1);
            }
        }

        return totalCharsWritten;
    }

    // [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))] TODO PERF: profile
    protected override async ValueTask<int> ReadAsyncCore(Memory<char> buffer, CancellationToken cancellationToken)
    {
        int totalCharsWritten = 0;

        while (true)
        {
            if (_offset >= _count && !_endOfStream)
            {
                _count = await _stream.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                _offset = 0;

                if (_count == 0)
                {
                    _endOfStream = true;
                    break;
                }
            }

            ReadOnlySpan<byte> byteSpan = new(_buffer, _offset, _count - _offset);
            Span<char> charSpan = buffer.Span.Slice(totalCharsWritten);

            if (!_preambleRead)
            {
                ReadPreamble(ref byteSpan);
            }

            OperationStatus status = Utf8.ToUtf16(
                byteSpan,
                charSpan,
                out int bytesConsumed,
                out int charsWritten,
                replaceInvalidSequences: true,
                isFinalBlock: _endOfStream
            );

            _offset += bytesConsumed;
            totalCharsWritten += charsWritten;

            if (status is OperationStatus.Done or OperationStatus.DestinationTooSmall)
            {
                break;
            }

            if (status == OperationStatus.NeedMoreData)
            {
                // Shift any leftover bytes to front
                int remaining = _count - _offset;
                if (remaining > 0)
                {
                    _buffer.AsSpan(_offset, remaining).CopyTo(_buffer);
                    _count = remaining;
                }
                else
                {
                    _count = 0;
                }

                _offset = 0;

                if (_endOfStream)
                    break;

                int bytesRead = await _stream
                    .ReadAsync(_buffer.AsMemory(_count), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    _endOfStream = true;
                    break;
                }

                _count += bytesRead;
            }
            else if (status == OperationStatus.InvalidData)
            {
                // Replace an invalid byte sequence with U+FFFD
                if (totalCharsWritten < buffer.Length)
                {
                    buffer.Span[totalCharsWritten++] = '\uFFFD';
                }
                else
                {
                    // User buffer full
                    break;
                }

                // Skip one byte to move past invalid data
                _offset += Math.Max(bytesConsumed, 1);
            }
        }

        return totalCharsWritten;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadPreamble(ref ReadOnlySpan<byte> byteSpan)
    {
        if (byteSpan is [0xEF, 0xBB, 0xBF, ..])
        {
            byteSpan = byteSpan[3..];
            _offset += 3;
        }

        _preambleRead = true;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_stream.CanSeek)
        {
            _stream.Position = 0;
            _offset = 0;
            _count = 0;
            _endOfStream = false;
            return true;
        }

        return false;
    }

    protected override void DisposeCore()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
        _stream.Dispose();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
        return _stream.DisposeAsync();
    }
}
