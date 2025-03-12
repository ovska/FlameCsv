using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
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
    private readonly T[]? _whitespace;
    private readonly SearchValues<T> _needsQuoting;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly Allocator<T> _allocator;
    private readonly bool _canVectorizeEscaping;

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
        _canVectorizeEscaping = _fieldQuoting is not CsvFieldQuoting.Never && dialect.IsAscii;
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

        if (skipEscaping || _fieldQuoting == CsvFieldQuoting.Never || !value.ContainsAny(_needsQuoting))
        {
            Writer.Advance(value.Length);
        }
        else
        {
            AdvanceAndHandleQuoting(destination, tokensWritten: value.Length);
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

    internal void AdvanceAndHandleQuoting(scoped Span<T> destination, int tokensWritten)
    {
        // empty writes don't need escaping
        if (tokensWritten == 0)
        {
            if (_fieldQuoting == CsvFieldQuoting.Always)
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

        // Value formatted, check if it needs to be wrapped in quotes
        if (_fieldQuoting == CsvFieldQuoting.Never)
        {
            Writer.Advance(tokensWritten);
            return;
        }

        if (_canVectorizeEscaping && tokensWritten >= Escape.MaskSize)
        {
            if (typeof(T) == typeof(char) && Vec256Char.IsSupported)
            {
                ref CsvFieldWriter<char> @this = ref Unsafe.As<CsvFieldWriter<T>, CsvFieldWriter<char>>(
                    ref Unsafe.AsRef(in this));
                Span<char> destinationChars = MemoryMarshal.Cast<T, char>(destination);

                if (@this._escape is null)
                {
                    SimdEscaperRFC<char, Vec256Char> escaper = new(
                        @this._quote,
                        @this._delimiter,
                        @this._newline.First,
                        @this._newline.Second);
                    @this.EscapeAndAdvance<SimdEscaperRFC<char, Vec256Char>, Vec256Char>(
                        in escaper,
                        destinationChars,
                        tokensWritten);
                }
                else
                {
                    SimdEscaperUnix<char, Vec256Char> escaper = new(
                        @this._escape.Value,
                        @this._quote,
                        @this._delimiter,
                        @this._newline);
                    @this.EscapeAndAdvance<SimdEscaperUnix<char, Vec256Char>, Vec256Char>(
                        in escaper,
                        destinationChars,
                        tokensWritten);
                }

                return;
            }

            if (typeof(T) == typeof(byte) && Vec256Byte.IsSupported)
            {
                ref CsvFieldWriter<byte> @this = ref Unsafe.As<CsvFieldWriter<T>, CsvFieldWriter<byte>>(
                    ref Unsafe.AsRef(in this));
                Span<byte> destinationBytes = MemoryMarshal.Cast<T, byte>(destination);

                if (@this._escape is null)
                {
                    SimdEscaperRFC<byte, Vec256Byte> escaper = new(
                        @this._quote,
                        @this._delimiter,
                        @this._newline.First,
                        @this._newline.Second);
                    @this.EscapeAndAdvance<SimdEscaperRFC<byte, Vec256Byte>, Vec256Byte>(
                        in escaper,
                        destinationBytes,
                        tokensWritten);
                }
                else
                {
                    SimdEscaperUnix<byte, Vec256Byte> escaper = new(
                        @this._escape.Value,
                        @this._quote,
                        @this._delimiter,
                        @this._newline);
                    @this.EscapeAndAdvance<SimdEscaperUnix<byte, Vec256Byte>, Vec256Byte>(
                        in escaper,
                        destinationBytes,
                        tokensWritten);
                }

                return;
            }
        }

        if (_escape is null)
        {
            EscapeAndAdvance(new RFC4180Escaper<T>(quote: _quote), destination, tokensWritten);
        }
        else
        {
            EscapeAndAdvance(new UnixEscaper<T>(quote: _quote, escape: _escape.Value), destination, tokensWritten);
        }
    }

    /// <summary>
    /// Attempts to escape the value written in the first <paramref name="tokensWritten"/> characters
    /// of <paramref name="destination"/>. Returns <see langword="false"/> if no escaping is needed
    /// and the writer was not advanced.
    /// </summary>
    /// <returns>True if the writer was advanced</returns>
    private void EscapeAndAdvance<TEscaper>(
        TEscaper escaper,
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
                shouldQuote = HasLeadingOrTrailingWhitespace(written);
                escapableCount = 0;
            }
        }

        if (!shouldQuote)
        {
            // no escaping, advance the original tokensWritten
            Writer.Advance(tokensWritten);
            return;
        }

        int escapedLength = EnsureCapacity(ref destination, ref written, tokensWritten, escapableCount);
        Escape.Scalar(ref escaper, written, destination[..escapedLength], escapableCount);
        Writer.Advance(escapedLength);
    }

    private void EscapeAndAdvance<TTokens, TVector>(
        ref readonly TTokens escaper,
        Span<T> destination,
        int tokensWritten)
        where TTokens : struct, ISimdEscaper<T, TVector>
        where TVector : struct, ISimdVector<T, TVector>
    {
        Debug.Assert(_fieldQuoting != CsvFieldQuoting.Never);
        Debug.Assert(destination.Length >= TVector.Count);
        Debug.Assert(tokensWritten >= TVector.Count);

        ReadOnlySpan<T> written = destination[..tokensWritten];
        uint[]? array = null;

        Span<uint> masks = Escape.GetMaskBuffer(tokensWritten, stackalloc uint[16], ref array);

        bool needsEscaping = Escape.IsRequired<T, TTokens, TVector>(written, masks, in escaper, out int quoteCount);

        if (needsEscaping || _fieldQuoting == CsvFieldQuoting.Always || HasLeadingOrTrailingWhitespace(written))
        {
            int escapedLength = EnsureCapacity(ref destination, ref written, tokensWritten, quoteCount);

            // write the escaped value to the destination buffer
            if (quoteCount == 0)
            {
                written.CopyTo(destination[1..]);
            }
            else
            {
                Escape.FromMasks(written, destination.Slice(1, escapedLength - 2), masks, escaper.Escape);
            }

            destination[escapedLength - 1] = _quote;
            destination[0] = _quote;
            Writer.Advance(escapedLength);
        }
        else
        {
            Writer.Advance(tokensWritten);
        }

        if (array is not null) ArrayPool<uint>.Shared.Return(array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasLeadingOrTrailingWhitespace(ReadOnlySpan<T> value)
    {
        Debug.Assert(value.Length > 0);

        if (_whitespace is not null)
        {
            ref T first = ref MemoryMarshal.GetReference(value);
            ref T last = ref Unsafe.Add(ref first, value.Length - 1);

            foreach (T token in _whitespace)
            {
                if (first == token || last == token)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Ensures the destination buffer from the inner writer is large enough to hold the quoted and escaped value.
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="written"></param>
    /// <param name="tokensWritten"></param>
    /// <param name="escapableCount"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int EnsureCapacity(
        ref Span<T> destination,
        ref ReadOnlySpan<T> written,
        int tokensWritten,
        int escapableCount)
    {
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

        return escapedLength;
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
