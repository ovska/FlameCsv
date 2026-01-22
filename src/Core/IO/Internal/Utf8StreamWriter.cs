using System.Buffers;
using System.Text;
using System.Text.Unicode;
using FlameCsv.Extensions;

namespace FlameCsv.IO.Internal;

#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

internal sealed class Utf8StreamWriter : CsvBufferWriter<char>
{
    private static readonly byte[] _utf8Preamble = Encoding.UTF8.GetPreamble();

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _bytes;
    private bool _writePreamble;

    public Utf8StreamWriter(Stream stream, in CsvIOOptions options, bool writePreamble = false)
        : base(in options)
    {
        Throw.IfNotWritable(stream);
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
        _writePreamble = writePreamble;
        _bytes = ArrayPool<byte>.Shared.Rent(options.BufferSize * 2);
    }

    protected override void DrainCore(ReadOnlyMemory<char> memory)
    {
        if (_writePreamble)
        {
            _stream.Write(_utf8Preamble);
            _writePreamble = false;
        }

        bool moreData;

        do
        {
            int bytesWritten = Transcode(ref memory, out moreData);

            if (bytesWritten != 0)
            {
                _stream.Write(_bytes, 0, bytesWritten);
            }
        } while (moreData);
    }

    protected override async ValueTask DrainAsyncCore(ReadOnlyMemory<char> memory, CancellationToken cancellationToken)
    {
        if (_writePreamble)
        {
            await _stream.WriteAsync(_utf8Preamble, cancellationToken).ConfigureAwait(false);
            _writePreamble = false;
        }

        bool moreData;

        do
        {
            int bytesWritten = Transcode(ref memory, out moreData);

            if (bytesWritten != 0)
            {
                await _stream.WriteAsync(_bytes, 0, bytesWritten, cancellationToken).ConfigureAwait(false);
            }
        } while (moreData);
    }

    protected override void FlushCore()
    {
        _stream.Flush();
    }

    protected override ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        return new ValueTask(_stream.FlushAsync(cancellationToken));
    }

    private int Transcode(ref ReadOnlyMemory<char> memory, out bool moreData)
    {
        moreData = false;
        ReadOnlySpan<char> chars = memory.Span;

        if (chars.IsEmpty)
        {
            return 0;
        }

        int totalRead = 0;
        int totalWritten = 0;

        while (!chars.IsEmpty)
        {
            OperationStatus status = Utf8.FromUtf16(
                chars,
                _bytes.AsSpan(totalWritten),
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
        return totalWritten;
    }

    protected override void DisposeCore()
    {
        ArrayPool<byte>.Shared.Return(_bytes);

        if (!_leaveOpen)
            _stream.Dispose();
    }

    protected override ValueTask DisposeCoreAsync()
    {
        ArrayPool<byte>.Shared.Return(_bytes);

        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}
