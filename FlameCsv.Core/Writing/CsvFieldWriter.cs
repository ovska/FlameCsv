using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Writing;

internal static class CsvFieldWriter
{
    public static CsvFieldWriter<char> Create(
        TextWriter textWriter,
        CsvOptions<char> options,
        int bufferSize)
    {
        return new CsvFieldWriter<char>(
            new CsvCharBufferWriter(textWriter, options._memoryPool, bufferSize),
            options);
    }

    public static CsvFieldWriter<byte> Create(
        PipeWriter pipeWriter,
        CsvOptions<byte> options)
    {
        return new CsvFieldWriter<byte>(
            new CsvByteBufferWriter(pipeWriter),
            options);
    }

    public static CsvFieldWriter<byte> Create(
        Stream stream,
        CsvOptions<byte> options,
        int bufferSize = -1,
        bool leaveOpen = false)
    {
        StreamPipeWriterOptions writerOptions = new(
            pool: options._memoryPool,
            minimumBufferSize: bufferSize,
            leaveOpen: leaveOpen);

        return new CsvFieldWriter<byte>(
            new CsvByteBufferWriter(PipeWriter.Create(stream, writerOptions)),
            options);
    }
}

/// <summary>
/// Writes CSV fields and handles escaping as needed.
/// This type is not intended to be used directly in user code, consider <see cref="CsvWriter{T}"/> instead.
/// </summary>
public readonly struct CsvFieldWriter<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// The <see cref="System.Buffers.IBufferWriter{T}"/> this instance writes to.
    /// </summary>
    public ICsvBufferWriter<T> Writer { get; }

    private readonly CsvOptions<T> _options;
    private readonly T _delimiter;
    private readonly T _quote;
    private readonly T _newline1;
    private readonly T _newline2;
    private readonly T? _escape;
    private readonly ReadOnlyMemory<T> _whitespace;
    private readonly int _newlineLength;
    private readonly SearchValues<T> _needsQuoting;

    public CsvFieldWriter(ICsvBufferWriter<T> writer, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);

        options.MakeReadOnly();

        Writer = writer;
        _options = options;

        ref readonly CsvDialect<T> dialect = ref options.Dialect;
        _delimiter = dialect.Delimiter;
        _quote = dialect.Quote;
        _escape = dialect.Escape;
        _whitespace = dialect.Whitespace;
        options.GetNewline(out _newline1, out _newline2, out _newlineLength, forWriting: true);
        _needsQuoting = dialect.NeedsQuoting;
    }

    /// <summary>
    /// Writes <paramref name="value"/> to the writer using <paramref name="converter"/>.
    /// </summary>
    public void WriteField<TValue>(CsvConverter<T, TValue> converter, TValue? value)
    {
        int tokensWritten;
        scoped Span<T> destination;

        if (value is not null || converter.HandleNull)
        {
            destination = Writer.GetSpan();

            while (!converter.TryFormat(destination, value!, out tokensWritten))
            {
                destination = Writer.GetSpan(destination.Length * 2);
            }
        }
        else
        {
            ReadOnlySpan<T> nullValue = _options.GetNullToken(typeof(TValue)).Span;
            destination = Writer.GetSpan(nullValue.Length);
            nullValue.CopyTo(destination);
            tokensWritten = nullValue.Length;
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            ThrowForInvalidTokensWritten(converter, tokensWritten, destination.Length);
        }

        AdvanceAndHandleQuoting(destination, tokensWritten);
    }

    /// <summary>
    /// Writes the text to the writer.
    /// </summary>
    /// <param name="value">Text to write</param>
    /// <param name="skipEscaping">Value is never escaped, use with care</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false)
    {
        int tokensWritten;
        scoped Span<T> destination = Writer.GetSpan();

        while (!_options.TryWriteChars(value, destination, out tokensWritten))
        {
            destination = Writer.GetSpan(destination.Length * 2);
        }

        // validate negative or too large tokensWritten in case of broken user-defined options
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            ThrowForInvalidTokensWritten(_options, tokensWritten, destination.Length);
        }

        if (!skipEscaping)
        {
            AdvanceAndHandleQuoting(destination, tokensWritten);
        }
        else
        {
            Writer.Advance(tokensWritten);
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
    public void WriteNewline()
    {
        Span<T> destination = Writer.GetSpan(_newlineLength);
        destination[0] = _newline1;

        if (_newlineLength == 2)
            destination[1] = _newline2;

        Writer.Advance(_newlineLength);
    }

    /// <summary>
    /// Writes the header with <paramref name="dematerializer"/> if <see cref="CsvOptions{T}.HasHeader"/> is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TryWriteHeader<TValue>(IDematerializer<T, TValue> dematerializer)
    {
        ArgumentNullException.ThrowIfNull(dematerializer);

        if (_options._hasHeader)
        {
            dematerializer.WriteHeader(in this);
        }
    }

    private void AdvanceAndHandleQuoting(
        scoped Span<T> destination,
        int tokensWritten)
    {
        // empty writes don't need escaping
        if (tokensWritten == 0)
        {
            if (_options._fieldEscaping == CsvFieldEscaping.AlwaysQuote)
            {
                // Ensure the buffer is large enough
                if (destination.Length < 2)
                    destination = Writer.GetSpan(2);

                destination[0] = _quote;
                destination[1] = _quote;
                Writer.Advance(2);
            }

            return;
        }

        // Value formatted, check if it needs to be wrapped in quotes
        if (_options._fieldEscaping != CsvFieldEscaping.Never)
        {
            if (_escape is null)
            {
                RFC4180Escaper<T> escaper = new(quote: _quote);
                if (TryEscapeAndAdvance(ref escaper, destination, tokensWritten))
                    return;
            }
            else
            {
                UnixEscaper<T> escaper = new(quote: _quote, escape: _escape.Value);
                if (TryEscapeAndAdvance(ref escaper, destination, tokensWritten))
                    return;
            }
        }

        Writer.Advance(tokensWritten);
    }

    /// <summary>
    /// Attempts to escape the value written in the first <paramref name="tokensWritten"/> characters
    /// of <paramref name="destination"/>. Returns <see langword="false"/> if no escaping is needed
    /// and the writer was not advanced.
    /// </summary>
    private bool TryEscapeAndAdvance<TEscaper>(
        ref TEscaper escaper,
        Span<T> destination,
        int tokensWritten)
        where TEscaper : struct, IEscaper<T>, allows ref struct
    {
        Debug.Assert(tokensWritten != 0);
        Debug.Assert(_options._fieldEscaping != CsvFieldEscaping.Never);

        ReadOnlySpan<T> written = destination[..tokensWritten];

        bool shouldQuote;
        int escapableCount;

        if (_options._fieldEscaping == CsvFieldEscaping.AlwaysQuote)
        {
            shouldQuote = true;
            escapableCount = escaper.CountEscapable(written);
        }
        else
        {
            int index = written.IndexOfAny(_needsQuoting);

            if (index != -1)
            {
                shouldQuote = true;
                escapableCount = escaper.CountEscapable(written[index..]);
            }
            else
            {
                shouldQuote = false;
                escapableCount = 0;

                if (!_whitespace.IsEmpty)
                {
                    ref T first = ref MemoryMarshal.GetReference(written);
                    ref T last = ref Unsafe.Add(ref first, written.Length - 1);

                    foreach (T token in _whitespace.Span)
                    {
                        if (first.Equals(token) || last.Equals(token))
                        {
                            shouldQuote = true;
                            break;
                        }
                    }
                }
            }
        }

        // if needed, escape/quote the field and adjust tokensWritten
        if (shouldQuote)
        {
            int escapedLength = tokensWritten + 2 + escapableCount;

            if (escapedLength <= destination.Length)
            {
                // Common case: escape directly to the destination buffer
                Escape.Field(ref escaper, written, destination[..escapedLength], escapableCount);
                Writer.Advance(escapedLength);
            }
            else
            {
                // Rare case: not enough space, escape as much as possible to
                // destination, then advance and write the leftovers
                Escape.FieldWithOverflow(
                    escaper: ref escaper,
                    writer: Writer,
                    source: written,
                    destination: destination,
                    specialCount: escapableCount,
                    allocator: _options._memoryPool);
            }

            return true;
        }

        return false;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalidTokensWritten(object culprit, int tokensWritten, int destinationLength)
    {
        string display = culprit is CsvConverter<T> ? "CsvConverter" : "CsvOptions";
        throw new InvalidOperationException(
            $"{display} ({culprit.GetType().FullName}) reported {tokensWritten} "
            + $"tokens written to a buffer of length {destinationLength}.");
    }
}
