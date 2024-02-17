using System.Buffers;
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
            pool: context.ArrayPool == ArrayPool<byte>.Shared ? MemoryPool<byte>.Shared : context.ArrayPool.AsMemoryPool(),
            minimumBufferSize: bufferSize,
            leaveOpen: leaveOpen);

        return new CsvFieldWriter<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(PipeWriter.Create(stream, options)),
            in context);
    }
}

public sealed class CsvFieldWriter<T, TWriter> : IDisposable
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
    private T[]? _array;

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

        // early exit for empty writes, like nulls or empty strings
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
                ReadOnlySpan<T> overflow = escaper.EscapeField(
                    source: written,
                    destination: destination,
                    specialCount: specialCount,
                    overflowBuffer: ref _array,
                    arrayPool: _arrayPool);

                // The whole of the span is filled, with the leftovers being written to the overflow
                _writer.Advance(destination.Length);

                overflow.CopyTo(_writer.GetSpan(overflow.Length));
                _writer.Advance(overflow.Length);
                return true;
            }

            // escape directly to the destination buffer and adjust the tokens written accordingly
            escaper.EscapeField(written, destination[..escapedLength], specialCount);
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
    public void WriteText(ReadOnlySpan<char> value) => _options.WriteChars(_writer, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _arrayPool.EnsureReturned(ref _array);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalidTokensWritten(CsvConverter<T> converter, int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"Converter ({converter.GetType().ToTypeString()}) reported {tokensWritten} " +
            $"tokens written to a buffer of length {destinationLength}.");
    }
}
