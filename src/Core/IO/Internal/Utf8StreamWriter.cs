using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using FlameCsv.Extensions;

namespace FlameCsv.IO.Internal;

internal sealed class Utf8StreamWriter : CsvBufferWriter<char>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly IMemoryOwner<byte> _byteBuffer;

    public Utf8StreamWriter(Stream stream, in CsvIOOptions options)
        : base(in options)
    {
        Throw.IfNotWritable(stream);
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
        _byteBuffer = options.EffectiveBufferPool.Rent<byte>(options.BufferSize * 2);
    }

    protected override void FlushCore(ReadOnlyMemory<char> memory)
    {
        bool moreData;

        do
        {
            ReadOnlyMemory<byte> bytes = Transcode(ref memory, out moreData);

            if (bytes.IsEmpty)
            {
                continue;
            }

            _stream.Write(bytes.Span);
        } while (moreData);
    }

    protected override ValueTask FlushAsyncCore(ReadOnlyMemory<char> memory, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> bytes = Transcode(ref memory, out bool moreData);

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
        CancellationToken cancellationToken = default
    )
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

        Memory<byte> rentedMemory = _byteBuffer.Memory;
        Span<byte> rentedSpan = rentedMemory.Span;

        while (!chars.IsEmpty)
        {
            OperationStatus status = Utf8.FromUtf16(
                chars,
                rentedSpan.Slice(totalWritten),
                out int charsRead,
                out int bytesWritten,
                replaceInvalidSequences: true,
                isFinalBlock: true // values should be self-contained
            );

            totalWritten += bytesWritten;
            totalRead += charsRead;

            if (status == OperationStatus.Done)
            {
                break;
            }

            if (status == OperationStatus.DestinationTooSmall)
            {
                moreData = true;
                break;
            }

            chars = chars.Slice(charsRead);
        }

        memory = memory.Slice(totalRead);
        return rentedMemory.Slice(0, totalWritten);
    }

    protected override void DisposeCore()
    {
        _byteBuffer.Dispose();

        if (!_leaveOpen)
            _stream.Dispose();
    }

    protected override ValueTask DisposeCoreAsync()
    {
        _byteBuffer.Dispose();

        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}
