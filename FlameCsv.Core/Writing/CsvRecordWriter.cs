using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Formatters;

namespace FlameCsv.Writing;

internal sealed class CsvRecordWriter<T, TWriter> : IAsyncDisposable
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>
{
    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _writer.NeedsFlush;
    }

    /// <summary>
    /// Observed exception when reading the data.
    /// </summary>
    public Exception? Exception { get; set; }

    private readonly TWriter _writer;
    private readonly CsvDialect<T> _dialect;
    private readonly ArrayPool<T> _arrayPool;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly CsvWriterOptions<T> _options;
    private T[]? _array;

    public CsvRecordWriter(
        TWriter writer,
        CsvWriterOptions<T> options)
    {
        _writer = writer;
        _dialect = new CsvDialect<T>(options);
        _arrayPool = options.ArrayPool.AllocatingIfNull();
        _fieldQuoting = options.FieldQuoting;
        _options = options;
    }

    public void WriteValue<TValue>(ICsvFormatter<T, TValue> formatter, [AllowNull] TValue value)
    {
        int tokensWritten;
        scoped Span<T> destination;

        // this whole branch is JITed out for value types
        if (value is null && !formatter.HandleNull)
        {
            ReadOnlySpan<T> nullValue = _options.GetNullToken(typeof(TValue)).Span;
            destination = _writer.GetSpan(nullValue.Length);
            nullValue.CopyTo(destination);
            tokensWritten = nullValue.Length;
        }
        else
        {
            destination = _writer.GetSpan();

            while (!formatter.TryFormat(value!, destination, out tokensWritten))
            {
                destination = _writer.GetSpan(destination.Length * 2);
            }
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            WriteHelpers.ThrowForInvalidTokensWritten(formatter, tokensWritten, destination.Length);
        }

        // early exit for empty writes, like nulls or empty strings
        if (tokensWritten == 0)
        {
            if (_fieldQuoting == CsvFieldQuoting.Always)
            {
                // Ensure the buffer is large enough
                if (destination.Length < 2)
                    destination = _writer.GetSpan(2);

                stackalloc T[] { _dialect.Quote, _dialect.Quote }.CopyTo(destination);
                _writer.Advance(2);
            }

            return;
        }

        // Value formatted, check if it needs to be wrapped in quotes
        if (_fieldQuoting != CsvFieldQuoting.Never)
        {
            if (_dialect.IsRFC4188Mode)
            {
                if (Escape(new RFC4188Escaper<T>(in _dialect), destination, tokensWritten))
                    return;
            }
            else
            {
                if (Escape(new UnixEscaper<T>(in _dialect), destination, tokensWritten))
                    return;
            }
        }

        _writer.Advance(tokensWritten);
    }

    private bool Escape<TEscaper>(
        in TEscaper escaper,
        Span<T> destination,
        int tokensWritten)
        where TEscaper : struct, IEscaper<T>
    {
        ReadOnlySpan<T> written = destination[..tokensWritten];

        bool shouldQuote;
        int specialCount;

        if (_fieldQuoting == CsvFieldQuoting.Always)
        {
            shouldQuote = true;
            specialCount = escaper.CountEscapable(written);
        }
        else
        {
            shouldQuote = escaper.NeedsEscaping(written, out specialCount);
        }

        // if needed, escape/quote the field and adjust tokensWritten
        if (shouldQuote)
        {
            int escapedLength = tokensWritten + 2 + specialCount;

            // If there isn't enough space to escape, escape partially to the overflow buffer
            // to avoid having to call the formatter again after growing the buffer
            if (escapedLength > destination.Length)
            {
                ReadOnlySpan<T> overflow = WriteUtil<T>.EscapeWithOverflow(
                    in escaper,
                    source: written,
                    destination: destination,
                    quoteCount: specialCount,
                    overflowBuffer: ref _array,
                    arrayPool: _arrayPool);

                // The whole of the span is filled, with the leftovers being written to the overflow
                _writer.Advance(destination.Length);

                overflow.CopyTo(_writer.GetSpan(overflow.Length));
                _writer.Advance(overflow.Length);
                return true;
            }

            // escape directly to the destination buffer and adjust the tokens written accordingly
            WriteUtil<T>.Escape(in escaper, written, destination[..escapedLength], specialCount);
            _writer.Advance(escapedLength);
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDelimiter()
    {
        Span<T> destination = _writer.GetSpan(1);
        destination[0] = _dialect.Delimiter;
        _writer.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNewline()
    {
        Span<T> destination = _writer.GetSpan(_dialect.Newline.Length);
        _dialect.Newline.Span.CopyTo(destination);
        _writer.Advance(_dialect.Newline.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(ReadOnlySpan<char> value) => _options.WriteChars(_writer, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return _writer.FlushAsync(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        _arrayPool.EnsureReturned(ref _array);
        return _writer.CompleteAsync(Exception);
    }
}
