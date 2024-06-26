﻿using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Writing;

internal static class CsvFieldWriter
{
    public static CsvFieldWriter<char, CsvCharBufferWriter> Create(
        TextWriter textWriter,
        CsvOptions<char> options)
    {
        return new CsvFieldWriter<char, CsvCharBufferWriter>(
            new CsvCharBufferWriter(textWriter, options._arrayPool),
            options);
    }

    public static CsvFieldWriter<byte, CsvByteBufferWriter> Create(
        PipeWriter pipeWriter,
        CsvOptions<byte> options)
    {
        return new CsvFieldWriter<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(pipeWriter),
            options);
    }

    public static CsvFieldWriter<byte, CsvByteBufferWriter> Create(
        Stream stream,
        CsvOptions<byte> options,
        int bufferSize = -1,
        bool leaveOpen = false)
    {
        StreamPipeWriterOptions writerOptions = new(
            pool: options._arrayPool.AsMemoryPool(),
            minimumBufferSize: bufferSize,
            leaveOpen: leaveOpen);

        return new CsvFieldWriter<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(PipeWriter.Create(stream, writerOptions)),
            options);
    }
}

public interface ICsvFieldWriter<T> where T : unmanaged, IEquatable<T>
{
    void WriteField<TValue>(CsvConverter<T, TValue> converter, [AllowNull] TValue value);
    void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false);
    void WriteDelimiter();
    void WriteNewline();
}

/// <summary>
/// Writes CSV fields and handles escaping as needed.
/// This type is not intended to be used directly in user code, consider <see cref="CsvWriter{T}"/> instead.
/// </summary>
public sealed class CsvFieldWriter<T, TWriter> : ICsvFieldWriter<T>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IBufferWriter<T>
{
    internal bool WriteHeader => _options._hasHeader;

    public TWriter Writer => _writer;

    private readonly TWriter _writer;
    private readonly CsvOptions<T> _options;

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly T _newline1;
    private readonly T _newline2;
    private readonly T? _escape;
    private readonly int _newlineLength;

    public CsvFieldWriter(TWriter writer, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        _writer = writer;
        _options = options;

        _delimiter = options._delimiter;
        _quote = options._quote;
        _escape = options._escape;
        options.GetNewline(out _newline1, out _newline2, out _newlineLength, forWriting: true);
    }

    /// <summary>
    /// Writes <paramref name="value"/> to the writer using <paramref name="converter"/>.
    /// </summary>
    public void WriteField<TValue>(CsvConverter<T, TValue> converter, [AllowNull] TValue value)
    {
        int tokensWritten;
        scoped Span<T> destination;

        if (value is not null || converter.HandleNull)
        {
            destination = _writer.GetSpan();

            while (!converter.TryFormat(destination, value!, out tokensWritten))
            {
                destination = _writer.GetSpan(destination.Length * 2);
            }
        }
        else
        {
            ReadOnlySpan<T> nullValue = _options.GetNullToken(typeof(TValue)).Span;
            destination = _writer.GetSpan(nullValue.Length);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false)
    {
        int tokensWritten;
        scoped Span<T> destination = _writer.GetSpan();

        while (!_options.TryWriteChars(value, destination, out tokensWritten))
        {
            destination = _writer.GetSpan(destination.Length * 2);
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
            _writer.Advance(tokensWritten);
        }
    }

    /// <summary>
    /// Writes <see cref="ICsvDialectOptions{T}.Delimiter"/> to the writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDelimiter()
    {
        Span<T> destination = _writer.GetSpan(1);
        destination[0] = _delimiter;
        _writer.Advance(1);
    }

    /// <summary>
    /// Writes <see cref="ICsvDialectOptions{T}.Newline"/> to the writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNewline()
    {
        Span<T> destination = _writer.GetSpan(_newlineLength);
        destination[0] = _newline1;

        if (_newlineLength == 2)
            destination[1] = _newline2;

        _writer.Advance(_newlineLength);
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
                    destination = _writer.GetSpan(2);

                destination[0] = _quote;
                destination[1] = _quote;
                _writer.Advance(2);
            }

            return;
        }

        // Value formatted, check if it needs to be wrapped in quotes
        if (_options._fieldEscaping != CsvFieldEscaping.Never)
        {
            if (_escape is null)
            {
                RFC4180Escaper<T> escaper = new(
                    delimiter: _delimiter,
                    quote: _quote,
                    newline1: _newline1,
                    newline2: _newline2,
                    newlineLength: _newlineLength,
                    whitespace: _options._whitespace);

                if (TryEscapeAndAdvance(in escaper, destination, tokensWritten))
                    return;
            }
            else
            {
                UnixEscaper<T> escaper = new(
                    delimiter: _delimiter,
                    quote: _quote,
                    escape: _escape.Value,
                    newline1: _newline1,
                    newline2: _newline2,
                    newlineLength: _newlineLength,
                    whitespace: _options._whitespace);

                if (TryEscapeAndAdvance(in escaper, destination, tokensWritten))
                    return;
            }
        }

        _writer.Advance(tokensWritten);
    }

    /// <summary>
    /// Attempts to escape the value written in the first <paramref name="tokensWritten"/> characters
    /// of <paramref name="destination"/>. Returns <see langword="false"/> if no escaping is needed
    /// and the writer was not advanced.
    /// </summary>
    private bool TryEscapeAndAdvance<TEscaper>(
        ref readonly TEscaper escaper,
        Span<T> destination,
        int tokensWritten)
        where TEscaper : struct, IEscaper<T>
    {
        Debug.Assert(_options._fieldEscaping != CsvFieldEscaping.Never);

        ReadOnlySpan<T> written = destination[..tokensWritten];

        bool shouldQuote;
        int specialCount;

        if (_options._fieldEscaping == CsvFieldEscaping.AlwaysQuote)
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

            if (escapedLength <= destination.Length)
            {
                // Common case: escape directly to the destination buffer
                escaper.EscapeField(written, destination[..escapedLength], specialCount);
                _writer.Advance(escapedLength);
            }
            else
            {
                // Rare case: not enough space, escape as much as possible to
                // destination, then advance and write the leftovers
                escaper.EscapeField(
                    writer: in _writer,
                    source: written,
                    destination: destination,
                    specialCount: specialCount,
                    arrayPool: _options._arrayPool);
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
            $"{display} ({culprit.GetType().ToTypeString()}) reported {tokensWritten} " +
            $"tokens written to a buffer of length {destinationLength}.");
    }
}
