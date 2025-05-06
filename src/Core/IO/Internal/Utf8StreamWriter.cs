using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using FlameCsv.Extensions;

namespace FlameCsv.IO.Internal;

internal sealed class Utf8StreamWriter : CsvBufferWriter<char>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private byte[] _byteBuffer;

    public Utf8StreamWriter(Stream stream, MemoryPool<char>? allocator, in CsvIOOptions options)
        : base(allocator ?? MemoryPool<char>.Shared, in options)
    {
        Throw.IfNotWritable(stream);
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
        _byteBuffer = ArrayPool<byte>.Shared.Rent(options.BufferSize * 2);
    }

    protected override void FlushCore(ReadOnlyMemory<char> memory)
    {
        bool moreData;

        do
        {
            var bytes = Transcode(ref memory, out moreData);

            if (bytes.IsEmpty)
            {
                continue;
            }

            _stream.Write(bytes.Span);
        } while (moreData);
    }

    protected override ValueTask FlushAsyncCore(ReadOnlyMemory<char> memory, CancellationToken cancellationToken)
    {
        var bytes = Transcode(ref memory, out bool moreData);

        if (bytes.IsEmpty)
        {
            return default;
        }

        // if this is the only chunk, write it directly
        if (!moreData)
        {
            return _stream.WriteAsync(bytes, cancellationToken);
        }

        return FlushAsyncAwaited(bytes, memory, cancellationToken);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask FlushAsyncAwaited(
        ReadOnlyMemory<byte> bytes,
        ReadOnlyMemory<char> chars,
        CancellationToken cancellationToken = default)
    {
        await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

        bool moreData;

        do
        {
            bytes = Transcode(ref chars, out moreData);

            if (bytes.IsEmpty)
            {
                continue;
            }

            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        } while (moreData);
    }

    private ReadOnlyMemory<byte> Transcode(ref ReadOnlyMemory<char> memory, out bool moreData)
    {
        moreData = false;
        ReadOnlySpan<char> chars = memory.Span;

        if (chars.IsEmpty)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        int totalRead = 0;
        int totalWritten = 0;

        while (!chars.IsEmpty)
        {
            OperationStatus status = Utf8.FromUtf16(
                chars,
                _byteBuffer.AsSpan(totalWritten),
                out int charsRead,
                out int bytesWritten,
                replaceInvalidSequences: true,
                isFinalBlock: true); // values should be self-contained

            totalWritten += bytesWritten;
            totalRead += charsRead;
            chars = chars.Slice(charsRead);

            if (status == OperationStatus.Done)
            {
                break;
            }

            if (status == OperationStatus.DestinationTooSmall)
            {
                moreData = true;
                break;
            }
        }

        memory = memory.Slice(totalRead);
        return _byteBuffer.AsMemory(0, totalWritten);
    }

    protected override void DisposeCore()
    {
        ArrayPool<byte>.Shared.Return(_byteBuffer);
        _byteBuffer = [];
        if (!_leaveOpen) _stream.Dispose();
    }

    protected override ValueTask DisposeCoreAsync()
    {
        ArrayPool<byte>.Shared.Return(_byteBuffer);
        _byteBuffer = [];
        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}
