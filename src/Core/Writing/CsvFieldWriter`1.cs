using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using FlameCsv.Writing.Escaping;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

/// <summary>
/// Writes CSV fields and handles escaping as needed.
/// </summary>
/// <remarks>
/// This type must be disposed to release rented memory.
/// </remarks>
[MustDisposeResource]
public readonly struct CsvFieldWriter<T> : IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// The <see cref="ICsvBufferWriter{T}"/> this instance writes to.
    /// </summary>
    public ICsvBufferWriter<T> Writer { get; }

    /// <summary>
    /// The options-instance for this writer.
    /// </summary>
    public CsvOptions<T> Options { get; }

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly bool _isCRLF;
    private readonly T? _escape;
    private readonly SearchValues<T> _needsQuoting;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly Allocator<T> _allocator;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public CsvFieldWriter(ICsvBufferWriter<T> writer, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);

        options.MakeReadOnly();

        Writer = writer;
        Options = options;

        _delimiter = T.CreateTruncating(options.Delimiter);
        _quote = T.CreateTruncating(options.Quote);
        _escape = options.Escape.HasValue ? T.CreateTruncating(options.Escape.Value) : null;
        _isCRLF = options.Newline.IsCRLF();
        _needsQuoting = options.NeedsQuoting;
        _fieldQuoting = options.FieldQuoting;
        _allocator = new MemoryPoolAllocator<T>(options.Allocator);
    }

    /// <summary>
    /// Writes <paramref name="value"/> to the writer using <paramref name="converter"/>.
    /// </summary>
    public void WriteField<TValue>(CsvConverter<T, TValue> converter, TValue? value)
    {
        int tokensWritten;
        scoped Span<T> destination;

        if (value is not null || converter.CanFormatNull)
        {
            destination = Writer.GetSpan();

            while (!converter.TryFormat(destination, value!, out tokensWritten))
            {
                destination = Writer.GetSpan(destination.Length * 2);
            }
        }
        else
        {
            ReadOnlySpan<T> nullValue = Options.GetNullSpan(typeof(TValue));
            destination = Writer.GetSpan(nullValue.Length);
            nullValue.CopyTo(destination);
            tokensWritten = nullValue.Length;
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            InvalidTokensWritten.Throw(converter, tokensWritten, destination.Length);
        }

        if (_fieldQuoting == CsvFieldQuoting.Never)
        {
            Writer.Advance(tokensWritten);
        }
        else
        {
            EscapeAndAdvance(destination, tokensWritten);
        }
    }

    /// <summary>
    /// Writes the text to the writer.
    /// </summary>
    /// <param name="value">Text to write</param>
    /// <param name="skipEscaping">Don't validate, escape or quote the written value in any way</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false)
    {
        // we are optimistic here with the sizeHint, assuming that most writes are ASCII
        int tokensWritten;
        scoped Span<T> destination = Writer.GetSpan(sizeHint: value.Length);

        while (!Transcode.TryFromChars(value, destination, out tokensWritten))
        {
            destination = Writer.GetSpan(destination.Length * 2);
        }

        Debug.Assert((uint)tokensWritten <= (uint)destination.Length);

        if (skipEscaping || _fieldQuoting == CsvFieldQuoting.Never)
        {
            Writer.Advance(tokensWritten);
        }
        else
        {
            EscapeAndAdvance(destination, tokensWritten);
        }
    }

    /// <summary>
    /// Writes raw value to the writer.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="skipEscaping">Don't validate, escape or quote the written value in any way</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(ReadOnlySpan<T> value, bool skipEscaping = false)
    {
        scoped Span<T> destination = Writer.GetSpan(sizeHint: value.Length);
        value.CopyTo(destination);

        if (skipEscaping || _fieldQuoting == CsvFieldQuoting.Never)
        {
            Writer.Advance(value.Length);
        }
        else
        {
            EscapeAndAdvance(destination, value.Length);
        }
    }

    /// <summary>
    /// Writes the null token for the given type to the writer.
    /// </summary>
    public void WriteNull<TValue>()
    {
        ReadOnlySpan<T> nullSpan = Options.GetNullSpan(typeof(TValue));

        if (!nullSpan.IsEmpty)
        {
            WriteRaw(nullSpan);
        }
    }

    /// <summary>
    /// Writes <see cref="CsvOptions{T}.Delimiter"/> to the writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDelimiter()
    {
        Span<T> destination = Writer.GetSpan(1);
        destination[0] = _delimiter;
        Writer.Advance(1);
    }

    /// <summary>
    /// Writes <see cref="CsvOptions{T}.Newline"/> to the writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void WriteNewline()
    {
        Span<T> destination = Writer.GetSpan(2);

        if (typeof(T) == typeof(byte))
        {
            ReadOnlySpan<byte> data = "\n\0\r\n"u8;
            ushort value = Unsafe.ReadUnaligned<ushort>(
                ref Unsafe.Add(
                    ref Unsafe.AsRef(in data[0]),
                    (uint)(sizeof(ushort) * Unsafe.BitCast<bool, byte>(_isCRLF))
                )
            );

            // ensure destination is large enough
            _ = destination[1];

            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref destination[0]), value);
        }
        else if (typeof(T) == typeof(char))
        {
            ReadOnlySpan<char> data = "\n\0\r\n".AsSpan();
            uint value = Unsafe.ReadUnaligned<uint>(
                ref Unsafe.Add(
                    ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in data[0])),
                    (uint)(sizeof(uint) * Unsafe.BitCast<bool, byte>(_isCRLF))
                )
            );

            // ensure destination is large enough
            _ = destination[1];
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref destination[0]), value);
        }
        else
        {
            throw Token<T>.NotSupported;
        }

        Writer.Advance(_isCRLF ? 2 : 1);
    }

    /// <summary>
    /// Advances the writer, escaping the written value if needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void EscapeAndAdvance(Span<T> destination, int tokensWritten)
    {
        if (_fieldQuoting == CsvFieldQuoting.Never)
        {
            Writer.Advance(tokensWritten);
            return;
        }

        // empty writes don't need escaping
        if (tokensWritten == 0)
        {
            if ((_fieldQuoting & CsvFieldQuoting.Empty) != 0)
            {
                // Ensure the buffer is large enough
                if (destination.Length < 2)
                {
                    destination = Writer.GetSpan(2);
                }

                destination[1] = _quote;
                destination[0] = _quote;
                Writer.Advance(2);
            }

            return;
        }

        scoped ReadOnlySpan<T> written = destination[..tokensWritten];

        bool shouldQuote;
        int escapableCount;

        RFC4180Escaper<T> escaper = new(_quote);

        if (_fieldQuoting == CsvFieldQuoting.Always)
        {
            shouldQuote = true;
            escapableCount = _escape is null ? escaper.CountEscapable(written) : CountRare(written);
        }
        else
        {
            int index = ((_fieldQuoting & CsvFieldQuoting.Auto) != 0) ? written.IndexOfAny(_needsQuoting) : -1;

            if (index != -1)
            {
                shouldQuote = true;

                ReadOnlySpan<T> tail = written[index..];
                escapableCount = _escape is null ? escaper.CountEscapable(tail) : CountRare(tail);
            }
            else
            {
                shouldQuote =
                    // help the branch predictor a bit
                    (_fieldQuoting & CsvFieldQuoting.LeadingOrTrailingSpaces) != 0
                    && (
                        // in net9, accessing the last item first omits a redundant bounds check
                        ((_fieldQuoting & CsvFieldQuoting.TrailingSpaces) != 0 && written[^1] == Whitespace)
                        || ((_fieldQuoting & CsvFieldQuoting.LeadingSpaces) != 0 && written[0] == Whitespace)
                    );
                escapableCount = 0;
            }
        }

        if (!shouldQuote)
        {
            // no escaping, advance the original tokensWritten
            Writer.Advance(tokensWritten);
            return;
        }

        int escapedLength = tokensWritten + 2 + escapableCount;

        // ensure the destination fits the unescaped buffer
        if (destination.Length < escapedLength)
        {
            // we cannot rely on the value being preserved if we get a new buffer from the bufferwriter.
            // Writer.GetSpan might not return the same memory, or it might be cleared before returning
            // so: copy the value into a temporary buffer from allocator, then escape it to a fresh destination buffer
            Span<T> temporary = _allocator.GetSpan(length: tokensWritten);
            written.CopyTo(temporary);
            written = temporary[..tokensWritten];
            destination = Writer.GetSpan(escapedLength);
        }

        if (_escape is null)
        {
            Escape.Scalar(escaper, written, destination[..escapedLength], escapableCount);
        }
        else
        {
            EscapeRare(written, destination[..escapedLength], escapableCount);
        }

        Writer.Advance(escapedLength);
    }

    // avoid inlining of rarer path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CountRare(ReadOnlySpan<T> span)
    {
        Debug.Assert(_escape.HasValue);
        return new UnixEscaper<T>(_quote, _escape.GetValueOrDefault()).CountEscapable(span);
    }

    // avoid inlining of rarer path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EscapeRare(ReadOnlySpan<T> source, Span<T> destination, int specialCount)
    {
        Debug.Assert(_escape.HasValue);
        Escape.Scalar(new UnixEscaper<T>(_quote, _escape.GetValueOrDefault()), source, destination, specialCount);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _allocator?.Dispose();
    }

    private static T Whitespace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            Unsafe.SizeOf<T>() switch
            {
                sizeof(byte) => Unsafe.BitCast<byte, T>((byte)' '),
                sizeof(char) => Unsafe.BitCast<char, T>(' '),
                _ => T.CreateTruncating(' '),
            };
    }
}

file static class InvalidTokensWritten
{
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(object source, int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"{source.GetType().FullName} reported {tokensWritten} tokens written to a buffer of length {destinationLength}."
        );
    }
}

internal ref struct WriterRecord<T> : IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly IBufferWriter<T> _writer;
    private readonly Allocator<T> _allocator;

    private Span<T> _buffer;
    private int _written;

    internal readonly Span<T> Destination
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            MemoryMarshal.CreateSpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (uint)_written),
                _buffer.Length - _written
            );
    }

    internal readonly ref T DestinationRef
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (uint)_written);
    }

    internal readonly int Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.Length - _written;
    }

    internal readonly IQuoter<T> Quoter { get; }

    private readonly byte _delimiter;
    private readonly bool _isCRLF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WriterRecord(
        CsvOptions<T> options,
        IBufferWriter<T> writer,
        int bufferSize,
        IQuoter<T> quoter,
        Allocator<T> allocator
    )
    {
        _options = options;
        _writer = writer;
        Quoter = quoter;
        _allocator = allocator;
        _buffer = writer.GetSpan(bufferSize);
    }

    public void WriteField<TValue>(CsvConverter<T, TValue> converter, TValue? value)
    {
        int tokensWritten;

        if (value is not null || converter.CanFormatNull)
        {
            while (!converter.TryFormat(Destination, value!, out tokensWritten))
            {
                Grow(Remaining * 2);
            }

            // validate negative or too large tokensWritten in case of broken user-defined formatters
            if ((uint)tokensWritten > (uint)Remaining)
            {
                InvalidTokensWritten.Throw(converter, tokensWritten, Remaining);
            }
        }
        // JITed out for value types
        else
        {
            ReadOnlySpan<T> nullValue = _options.GetNullSpan(typeof(TValue));

            if (Remaining < nullValue.Length)
            {
                Grow(nullValue.Length);
            }

            nullValue.CopyTo(Destination);
            tokensWritten = nullValue.Length;
        }

        QuotingResult result = Quoter.NeedsQuoting(Destination[..tokensWritten]);

        if (result.NeedsQuoting)
        {
            Escape(Destination, tokensWritten, result);
        }
        else
        {
            _written += tokensWritten;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false)
    {
        // we are optimistic here with the sizeHint, assuming that most writes are ASCII
        int tokensWritten;

        while (!Transcode.TryFromChars(value, Destination, out tokensWritten))
        {
            Grow(Transcode.GetMaxTranscodedSize<T>(value));
        }

        if (!skipEscaping)
        {
            QuotingResult result = Quoter.NeedsQuoting(Destination[..tokensWritten]);

            if (result.NeedsQuoting)
            {
                Escape(Destination, tokensWritten, result);
                return;
            }
        }

        _written += tokensWritten;
    }

    public void WriteRaw(ReadOnlySpan<T> value, bool skipEscaping = false)
    {
        int length = value.Length;

        if (Remaining < length)
        {
            Grow(length);
        }

        value.CopyTo(Destination);

        if (!skipEscaping)
        {
            QuotingResult result = Quoter.NeedsQuoting(value);

            if (result.NeedsQuoting)
            {
                Escape(Destination, length, result);
                return;
            }
        }

        _written += length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDelimiter()
    {
        if (Remaining == 0)
        {
            Grow(1);
        }

        DestinationRef = T.CreateTruncating(_delimiter);
        _written++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNewline()
    {
        if (Remaining < 2)
        {
            Grow(2);
        }

        if (typeof(T) == typeof(byte))
        {
            ReadOnlySpan<byte> data = "\n\0\r\n"u8;
            ushort value = Unsafe.ReadUnaligned<ushort>(
                ref Unsafe.Add(
                    ref Unsafe.AsRef(in data[0]),
                    (uint)(sizeof(ushort) * Unsafe.BitCast<bool, byte>(_isCRLF))
                )
            );

            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref DestinationRef), value);
        }
        else if (typeof(T) == typeof(char))
        {
            ReadOnlySpan<char> data = "\n\0\r\n".AsSpan();
            uint value = Unsafe.ReadUnaligned<uint>(
                ref Unsafe.Add(
                    ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in data[0])),
                    (uint)(sizeof(uint) * Unsafe.BitCast<bool, byte>(_isCRLF))
                )
            );

            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref DestinationRef), value);
        }
        else
        {
            throw Token<T>.NotSupported;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Grow(int previousSize)
    {
        _writer.Advance(_written);
        _written = 0;

        int requestedSize = previousSize * 2;
        _buffer = _writer.GetSpan(requestedSize);

        if (_buffer.Length < requestedSize)
        {
            ThrowHelper.InvalidBuffer(requestedSize, _buffer.Length, _writer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EscapeIfNeeded(Span<T> destination, int written)
    {
        QuotingResult result = Quoter.NeedsQuoting(destination[..written]);

        if (result.NeedsQuoting)
        {
            Escape(destination, written, result);
        }
        else
        {
            _written += written;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Escape(Span<T> destination, int written, QuotingResult result)
    {
        int escapedLength = written + 2 + result.SpecialCount;

        // source containing the original written data
        Span<T> source;

        // rare case: buffer is too small to fit the escaped value
        if (destination.Length < escapedLength)
        {
            Span<T> temp = _allocator.GetSpan(written);
            destination.Slice(0, written).CopyTo(temp);
            source = temp[..written];

            _writer.Advance(_written);
            _written = 0;

            destination = _writer.GetSpan(escapedLength);

            if (destination.Length < escapedLength)
            {
                ThrowHelper.InvalidBuffer(escapedLength, destination.Length, _writer);
            }
        }
        else
        {
            source = destination.Slice(0, written);
        }

        Debug.Assert(destination.Length >= source.Length + result.SpecialCount + 2, "Destination buffer is too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory"
        );

        // Work backwards as the source and destination buffers might overlap
        nint srcRemaining = source.Length - 1;
        nint dstRemaining = destination.Length - 1;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        int quoteCount = result.SpecialCount;
        T quote = Quoter.Quote;

        Unsafe.Add(ref dst, dstRemaining) = quote;
        dstRemaining--;

        // if there are no quotes to escape, just wrap in quotes and copy
        if (quoteCount == 0)
            goto End;

        int lastIndex = result.LastIndex ?? source.LastIndexOf(quote);

        // if we found nothing, there are no special chars, or it was the last token
        if ((uint)lastIndex < srcRemaining)
        {
            nint nonSpecialCount = srcRemaining - lastIndex + 1;
            Copy(ref src, (uint)lastIndex, ref dst, (nuint)(dstRemaining - nonSpecialCount + 1), (uint)nonSpecialCount);

            srcRemaining -= nonSpecialCount;
            dstRemaining -= nonSpecialCount;
            Unsafe.Add(ref dst, dstRemaining) = quote;
            dstRemaining--;

            if (--quoteCount == 0)
                goto End;
        }

        while (srcRemaining >= 0)
        {
            if (quote == Unsafe.Add(ref src, srcRemaining))
            {
                Unsafe.Add(ref dst, dstRemaining) = Unsafe.Add(ref src, srcRemaining);
                Unsafe.Add(ref dst, dstRemaining - 1) = quote;

                srcRemaining -= 1;
                dstRemaining -= 2;

                if (--quoteCount == 0)
                {
                    goto End;
                }
            }
            else
            {
                Unsafe.Add(ref dst, dstRemaining) = Unsafe.Add(ref src, srcRemaining);
                srcRemaining--;
                dstRemaining--;
            }
        }

        End:
        Copy(ref src, 0, ref dst, 1, (uint)srcRemaining + 1u);

        // the final quote must!! be written last since src and dst likely occupy the same memory region
        dst = quote;

        _written += escapedLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, nuint srcIndex, ref T dst, nuint dstIndex, uint length)
        {
            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length
            );
        }
    }

    public void Dispose()
    {
        _writer.Advance(_written);
        _written = 0;
    }
}

file static class ThrowHelper
{
    public static void InvalidBuffer(int requestedLength, int actualLength, object bufferWriter)
    {
        throw new InvalidOperationException(
            $"The buffer returned by the IBufferWriter is too small. Requested length: {requestedLength}, actual length: {actualLength}. BufferWriter: {bufferWriter.GetType().FullName}"
        );
    }
}
