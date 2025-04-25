using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.IO;
using FlameCsv.Reading.Internal;
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
    private readonly SearchValues<T> _needsQuoting;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly Allocator<T> _allocator;

    private readonly RFC4180Escaper<T> _rfc4180Escaper;
    private readonly UnixEscaper<T> _unixEscaper;

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
        _newline = dialect.Newline;
        if (_newline.IsEmpty) _newline = NewlineBuffer<T>.CRLF;
        _needsQuoting = dialect.NeedsQuoting;
        _fieldQuoting = options.FieldQuoting;
        _allocator = new MemoryPoolAllocator<T>(options.Allocator);
        _rfc4180Escaper = new RFC4180Escaper<T>(quote: _quote);
        _unixEscaper = new UnixEscaper<T>(quote: _quote, escape: _escape ?? _quote);
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

        if (_escape is null)
        {
            EscapeAndAdvance(in _rfc4180Escaper, destination, tokensWritten);
        }
        else
        {
            EscapeAndAdvance(in _unixEscaper, destination, tokensWritten);
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

        if (skipEscaping || _fieldQuoting is CsvFieldQuoting.Never)
        {
            Writer.Advance(tokensWritten);
        }
        else
        {
            if (_escape is null)
            {
                EscapeAndAdvance(_rfc4180Escaper, destination, value.Length);
            }
            else
            {
                EscapeAndAdvance(_unixEscaper, destination, value.Length);
            }
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

        if (skipEscaping || _fieldQuoting is CsvFieldQuoting.Never)
        {
            Writer.Advance(value.Length);
        }
        else
        {
            if (_escape is null)
            {
                EscapeAndAdvance(new RFC4180Escaper<T>(quote: _quote), destination, value.Length);
            }
            else
            {
                EscapeAndAdvance(new UnixEscaper<T>(quote: _quote, escape: _escape.Value), destination, value.Length);
            }
        }
    }

    /// <summary>
    /// Writes the null token for the given type to the writer.
    /// </summary>
    public void WriteNull<TValue>()
    {
        ReadOnlyMemory<T> nullToken = Options.GetNullToken(typeof(TValue));

        if (nullToken.IsEmpty)
        {
            return;
        }

        WriteRaw(nullToken.Span);
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
        if (_newline.Length == 2) destination[1] = _newline.Second;
        destination[0] = _newline.First;
        Writer.Advance(_newline.Length);
    }

    /// <summary>
    /// See <see cref="CsvFieldWritingExtensions"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EscapeAndAdvanceExternal(Span<T> destination, int tokensWritten)
    {
        if (_escape is null)
        {
            EscapeAndAdvance(in _rfc4180Escaper, destination, tokensWritten);
        }
        else
        {
            EscapeAndAdvance(in _unixEscaper, destination, tokensWritten);
        }
    }

    /// <summary>
    /// Attempts to escape the value written in the first <paramref name="tokensWritten"/> characters
    /// of <paramref name="destination"/>. Returns <see langword="false"/> if no escaping is needed
    /// and the writer was not advanced.
    /// </summary>
    /// <returns>True if the writer was advanced</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EscapeAndAdvance<TEscaper>(
        in TEscaper escaper,
        Span<T> destination,
        int tokensWritten)
        where TEscaper : struct, IEscaper<T>, allows ref struct
    {
        if (_fieldQuoting is CsvFieldQuoting.Never)
        {
            Writer.Advance(tokensWritten);
            return;
        }

        // empty writes don't need escaping
        if (tokensWritten == 0)
        {
            if (_fieldQuoting is CsvFieldQuoting.Always or CsvFieldQuoting.Empty)
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

        if (_fieldQuoting is CsvFieldQuoting.Always)
        {
            shouldQuote = true;
            escapableCount = escaper.CountEscapable(written);
        }
        else
        {
            int index = written.LastIndexOfAny(_needsQuoting);

            if (index != -1)
            {
                shouldQuote = true;
                escapableCount = escaper.CountEscapable(written[index..]);
            }
            else
            {
                if (_fieldQuoting == CsvFieldQuoting.LeadingOrTrailingSpaces)
                {
                    shouldQuote =
                        written[0] == T.CreateTruncating(' ') ||
                        written[^1] == T.CreateTruncating(' ');
                }
                else
                {
                    shouldQuote = false;
                }

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
            // GetSpan might not return the same memory, or it might be cleared before returning
            // so: copy the value into a temporary buffer, then escape it to a fresh destination buffer
            Span<T> temporary = _allocator.GetSpan(length: tokensWritten);
            written.CopyTo(temporary);
            written = temporary[..tokensWritten];
            destination = Writer.GetSpan(escapedLength);
        }

        Escape.Scalar(in escaper, written, destination[..escapedLength], escapableCount);
        Writer.Advance(escapedLength);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _allocator?.Dispose();
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
