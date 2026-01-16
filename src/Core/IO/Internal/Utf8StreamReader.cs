using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using FlameCsv.Extensions;

namespace FlameCsv.IO.Internal;

internal sealed class Utf8StreamReader : CsvBufferReader<char>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly IMemoryOwner<byte> _bufferOwner;
    private int _count;
    private int _offset;
    private bool _endOfStream;
    private bool _preambleRead;

    public Utf8StreamReader(Stream stream, in CsvIOOptions options)
        : base(in options)
    {
        Throw.IfNotReadable(stream);
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
        _bufferOwner = options.EffectiveBufferPool.Rent<byte>(options.BufferSize);
    }

    protected override int ReadCore(Span<char> buffer)
    {
        int totalCharsWritten = 0;

        Span<byte> localBuffer = _bufferOwner.Memory.Span;

        while (true)
        {
            if (_offset >= _count && !_endOfStream)
            {
                _count = _stream.Read(localBuffer);
                _offset = 0;

                if (_count == 0)
                {
                    _endOfStream = true;
                    break;
                }
            }

            ReadOnlySpan<byte> byteSpan = localBuffer.Slice(_offset, _count - _offset);
            Span<char> charSpan = buffer.Slice(totalCharsWritten);

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

            Check.Equal(status, OperationStatus.NeedMoreData);

            if (status == OperationStatus.NeedMoreData)
            {
                // Shift any leftover bytes to front
                int remaining = _count - _offset;
                if (remaining > 0)
                {
                    localBuffer.Slice(_offset, remaining).CopyTo(localBuffer);
                    _count = remaining;
                }
                else
                {
                    _count = 0;
                }

                _offset = 0;

                if (_endOfStream)
                    break;

                int bytesRead = _stream.Read(localBuffer.Slice(_count));

                if (bytesRead == 0)
                {
                    _endOfStream = true;
                    break;
                }

                _count += bytesRead;
            }
        }

        return totalCharsWritten;
    }

    // [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))] TODO PERF: profile
    protected override async ValueTask<int> ReadAsyncCore(Memory<char> buffer, CancellationToken cancellationToken)
    {
        int totalCharsWritten = 0;

        Memory<byte> localBuffer = _bufferOwner.Memory;

        while (true)
        {
            if (_offset >= _count && !_endOfStream)
            {
                _count = await _stream.ReadAsync(localBuffer, cancellationToken).ConfigureAwait(false);
                _offset = 0;

                if (_count == 0)
                {
                    _endOfStream = true;
                    break;
                }
            }

            ReadOnlySpan<byte> byteSpan = localBuffer.Slice(_offset, _count - _offset).Span;
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

            Check.Equal(status, OperationStatus.NeedMoreData);

            if (status == OperationStatus.NeedMoreData)
            {
                // Shift any leftover bytes to front
                int remaining = _count - _offset;
                if (remaining > 0)
                {
                    localBuffer.Slice(_offset, remaining).CopyTo(localBuffer);
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
                    .ReadAsync(localBuffer.Slice(_count), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    _endOfStream = true;
                    break;
                }

                _count += bytesRead;
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

    protected override bool TryResetCore()
    {
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
        _bufferOwner.Dispose();

        if (!_leaveOpen)
            _stream.Dispose();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _bufferOwner.Dispose();
        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}
