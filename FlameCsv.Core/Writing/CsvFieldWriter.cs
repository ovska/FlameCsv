using System.Buffers;
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
        in CsvWritingContext<char> context)
    {
        return new CsvFieldWriter<char, CsvCharBufferWriter>(
            new CsvCharBufferWriter(textWriter, context.ArrayPool),
            in context);
    }

    public static CsvFieldWriter<byte, CsvByteBufferWriter> Create(
        PipeWriter pipeWriter,
        in CsvWritingContext<byte> context)
    {
        return new CsvFieldWriter<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(pipeWriter),
            in context);
    }

    public static CsvFieldWriter<byte, CsvByteBufferWriter> Create(
        Stream stream,
        in CsvWritingContext<byte> context,
        int bufferSize = -1,
        bool leaveOpen = false)
    {
        StreamPipeWriterOptions options = new(
            pool: context.ArrayPool.AsMemoryPool(),
            minimumBufferSize: bufferSize,
            leaveOpen: leaveOpen);

        return new CsvFieldWriter<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(PipeWriter.Create(stream, options)),
            in context);
    }
}

public sealed class CsvFieldWriter<T, TWriter>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IBufferWriter<T>
{
    internal bool WriteHeader { get; }

    public TWriter Writer => _writer;

    private readonly TWriter _writer;
    private readonly CsvDialect<T> _dialect;
    private readonly ArrayPool<T> _arrayPool;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly CsvOptions<T> _options;

    public CsvFieldWriter(TWriter writer, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        _writer = writer;
        _dialect = new CsvDialect<T>(options);
        _arrayPool = options.ArrayPool.AllocatingIfNull();
        _fieldQuoting = options.FieldQuoting;
        _options = options;
        WriteHeader = options.HasHeader;
    }

    internal CsvFieldWriter(
        TWriter writer,
        in CsvWritingContext<T> context)
    {
        _writer = writer;
        _dialect = context.Dialect;
        _arrayPool = context.ArrayPool;
        _fieldQuoting = context.FieldQuoting;
        _options = context.Options;
        WriteHeader = context.HasHeader;
    }

    /// <summary>
    /// Writes <paramref name="value"/> to the writer using <paramref name="converter"/>.
    /// </summary>
    public void WriteField<TValue>(CsvConverter<T, TValue> converter, [AllowNull] TValue value)
    {
        int tokensWritten;
        scoped Span<T> destination;

        // this whole branch is JITed out for value types
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

        // empty writes don't need escaping, like nulls or empty strings
        if (tokensWritten == 0)
        {
            if (_fieldQuoting == CsvFieldQuoting.Always)
            {
                // Ensure the buffer is large enough
                if (destination.Length < 2)
                    destination = _writer.GetSpan(2);

                destination[0] = _dialect.Quote;
                destination[1] = _dialect.Quote;
                _writer.Advance(2);
            }

            return;
        }

        // Value formatted, check if it needs to be wrapped in quotes
        if (_fieldQuoting != CsvFieldQuoting.Never)
        {
            if (_dialect.IsRFC4180Mode)
            {
                if (TryEscapeAndAdvance(new RFC4180Escaper<T>(in _dialect), destination, tokensWritten))
                    return;
            }
            else
            {
                if (TryEscapeAndAdvance(new UnixEscaper<T>(in _dialect), destination, tokensWritten))
                    return;
            }
        }

        _writer.Advance(tokensWritten);
    }

    /// <summary>
    /// Attempts to escape the value written in the first <paramref name="tokensWritten"/> characters
    /// of <paramref name="destination"/>. Returns <see langword="false"/> if no escaping is done,
    /// and the writer has not been advanced.
    /// </summary>
    private bool TryEscapeAndAdvance<TEscaper>(
        in TEscaper escaper,
        Span<T> destination,
        int tokensWritten)
        where TEscaper : struct, IEscaper<T>
    {
        Debug.Assert(_fieldQuoting != CsvFieldQuoting.Never);

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
                    arrayPool: _arrayPool);
            }
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
    public void WriteText(ReadOnlySpan<char> value) => _options.WriteText(_writer, value);

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalidTokensWritten(CsvConverter<T> converter, int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"Converter ({converter.GetType().ToTypeString()}) reported {tokensWritten} " +
            $"tokens written to a buffer of length {destinationLength}.");
    }
}
