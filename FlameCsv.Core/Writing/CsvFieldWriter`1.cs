using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.IO;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

/// <summary>
/// Writes CSV fields and handles escaping as needed.
/// </summary>
/// <remarks>
/// This type must be disposed to release rented memory.
/// </remarks>
[MustDisposeResource]
public readonly struct CsvFieldWriter<T> : IDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// The <see cref="ICsvPipeWriter{T}"/> this instance writes to.
    /// </summary>
    public ICsvPipeWriter<T> Writer { get; }

    /// <summary>
    /// The options-instance for this writer.
    /// </summary>
    public CsvOptions<T> Options { get; }

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly NewlineBuffer<T> _newline;
    private readonly T? _escape;
    private readonly T[]? _whitespace;
    private readonly SearchValues<T> _needsQuoting;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly Allocator<T> _allocator;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public CsvFieldWriter(ICsvPipeWriter<T> writer, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);

        options.MakeReadOnly();

        Writer = writer;
        Options = options;

        ref readonly CsvDialect<T> dialect = ref options.Dialect;
        _delimiter = dialect.Delimiter;
        _quote = dialect.Quote;
        _escape = dialect.Escape;
        _whitespace = dialect.GetWhitespaceArray();
        _newline = dialect.GetNewlineOrDefault(forWriting: true);
        _needsQuoting = dialect.NeedsQuoting;
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
            ReadOnlySpan<T> nullValue = Options.GetNullToken(typeof(TValue)).Span;
            destination = Writer.GetSpan(nullValue.Length);
            nullValue.CopyTo(destination);
            tokensWritten = nullValue.Length;
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            InvalidTokensWritten.Throw(converter, tokensWritten, destination.Length);
        }

        AdvanceAndHandleQuoting(destination, tokensWritten);
    }

    /// <summary>
    /// Writes the text to the writer.
    /// </summary>
    /// <param name="value">Text to write</param>
    /// <param name="skipEscaping">Don't validate, escape or quote the written value in any way</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false)
    {
        int tokensWritten;
        scoped Span<T> destination = Writer.GetSpan();

        while (!Options.TryWriteChars(value, destination, out tokensWritten))
        {
            destination = Writer.GetSpan(destination.Length * 2);
        }

        // validate negative or too large tokensWritten in case of broken user-defined options
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            InvalidTokensWritten.Throw(Options, tokensWritten, destination.Length);
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
    /// Writes raw value to the writer.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="skipEscaping">Don't validate, escape or quote the written value in any way</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(ReadOnlySpan<T> value, bool skipEscaping = false)
    {
        scoped Span<T> destination = Writer.GetSpan(value.Length);
        value.CopyTo(destination);

        if (!skipEscaping)
        {
            AdvanceAndHandleQuoting(destination, tokensWritten: value.Length);
        }
        else
        {
            Writer.Advance(value.Length);
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
        Span<T> destination = Writer.GetSpan(_newline.Length);
        destination[_newline.Length - 1] = _newline.Second;
        destination[0] = _newline.First;
        Writer.Advance(_newline.Length);
    }

    private void AdvanceAndHandleQuoting(scoped Span<T> destination, int tokensWritten)
    {
        // empty writes don't need escaping
        if (tokensWritten == 0)
        {
            if (_fieldQuoting == CsvFieldQuoting.Always)
            {
                // Ensure the buffer is large enough
                if (destination.Length < 2)
                    destination = Writer.GetSpan(2);

                destination[1] = _quote;
                destination[0] = _quote;
                Writer.Advance(2);
            }

            return;
        }

        // Value formatted, check if it needs to be wrapped in quotes
        if (_fieldQuoting != CsvFieldQuoting.Never)
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
    /// <returns>True if the writer was advanced</returns>
    private bool TryEscapeAndAdvance<TEscaper>(
        ref TEscaper escaper,
        Span<T> destination,
        int tokensWritten)
        where TEscaper : struct, IEscaper<T>, allows ref struct
    {
        Debug.Assert(tokensWritten != 0);
        Debug.Assert(_fieldQuoting != CsvFieldQuoting.Never);

        ReadOnlySpan<T> written = destination[..tokensWritten];

        bool shouldQuote;
        int escapableCount;

        if (_fieldQuoting == CsvFieldQuoting.Always)
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

                if (_whitespace is not null)
                {
                    ref T first = ref MemoryMarshal.GetReference(written);
                    ref T last = ref Unsafe.Add(ref first, written.Length - 1);

                    foreach (T token in _whitespace)
                    {
                        if (first == token || last == token)
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

            // ensure the destination fits the unescaped buffer
            if (destination.Length < escapedLength)
            {
                // GetSpan might not return the same memory, or it might be cleared depending on implementation
                // copy the value into a temporary buffer, then escape it to a fresh destination
                Span<T> temporary = _allocator.GetSpan(length: tokensWritten);
                written.CopyTo(temporary);
                written = temporary[..tokensWritten];
                destination = Writer.GetSpan(escapedLength);
            }

            Escape.Field(ref escaper, written, destination[..escapedLength], escapableCount);
            Writer.Advance(escapedLength);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Escape-aware wrapper around <see cref="IBufferWriter{T}.GetSpan"/>.
    /// </summary>
    internal ref struct BufferScope : IDisposable
    {
        public Span<T> Destination { get; private set; }
        private Span<T> _buffer; // quote-aware buffer
        private int _written;
        private byte _state;

        private readonly ICsvPipeWriter<T> _writer;
        private readonly T _quote;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferScope(ref readonly CsvFieldWriter<T> writer)
        {
            _writer = writer.Writer;
            _quote = writer._quote;

            if (writer._fieldQuoting is CsvFieldQuoting.Always)
            {
                _buffer = writer.Writer.GetSpan(2);
                Destination = _buffer[1..^1]; // minimize copying
            }
            else
            {
                _buffer = writer.Writer.GetSpan();
                Destination = _buffer;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> Grow(bool copy = false)
        {
            Debug.Assert(_state == 0);

            Span<T> newBuffer = _writer.GetSpan(_buffer.Length);

            // no always quoting
            if (_buffer.Length == Destination.Length)
            {
                if (copy) _buffer.CopyTo(newBuffer);
                _buffer = newBuffer;
                Destination = newBuffer;
            }
            else
            {
                if (copy) _buffer.CopyTo(newBuffer[1..]);
                _buffer = newBuffer;
                Destination = newBuffer[1..^1];
            }

            return Destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int tokensWritten, object source)
        {
            Debug.Assert(_state == 0);

            // validate negative or too large tokensWritten in case of broken user-defined formatters
            if ((uint)tokensWritten > (uint)Destination.Length)
            {
                InvalidTokensWritten.Throw(source, tokensWritten, Destination.Length);
            }

            _written = tokensWritten;
            _state = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_state != 1) return;
            _state = 2;

            if (_buffer.Length != Destination.Length)
            {
                _buffer[0] = _quote;
                _buffer[^1] = _quote;
                _writer.Advance(_written + 2);
            }
            else if (_written != 0)
            {
                _writer.Advance(_written);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _allocator.Dispose();
    }
}

file static class InvalidTokensWritten
{
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(object source, int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"{source.GetType().FullName} reported {tokensWritten} tokens written to a buffer of length {destinationLength}.");
    }
}
