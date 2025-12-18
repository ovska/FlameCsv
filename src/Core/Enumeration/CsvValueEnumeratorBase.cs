using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// An enumerator that parses CSV records as <typeparamref name="TValue"/>.
/// </summary>
/// <remarks>
/// If the options are configured to read a header record, it will be processed first before any records are yielded.<br/>
/// This class is not thread-safe, and should not be used concurrently.<br/>
/// The enumerator should always be disposed after use, either explicitly or using <c>foreach</c>.
/// </remarks>
[MustDisposeResource]
public abstract class CsvValueEnumeratorBase<T, TValue>
    : CsvEnumeratorBase<T>,
        IEnumerator<TValue>,
        IAsyncEnumerator<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Value parsed from the current CSV record.
    /// </summary>
    public TValue Current { get; private set; }

    object? IEnumerator.Current => Current;

    void IEnumerator.Reset()
    {
        ResetCore();
        Current = default!;
        _materializer = null;
        Headers = default;
    }

    private readonly CsvExceptionHandler<T>? _exceptionHandler;
    private IMaterializer<T, TValue>? _materializer;

    /// <summary>
    /// Returns the headers in the CSV.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// A non-null value is set when <see cref="CsvOptions{T}.HasHeader"/> is <c>false</c>.
    /// </exception>
    protected ImmutableArray<string> Headers
    {
        get => field;
        private set
        {
            if (!value.IsDefault && !Options.HasHeader)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (value.IsDefaultOrEmpty || !Headers.AsSpan().SequenceEqual(value.AsSpan(), StringComparer.Ordinal))
            {
                field = value;
                _materializer = null;
            }
        }
    }

    /// <inheritdoc />
    protected override ImmutableArray<string> GetHeader() => Headers;

    /// <inheritdoc />
    protected override void ResetHeader() => Headers = default;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvValueEnumeratorBase{T, TValue}"/>.
    /// </summary>
    /// <param name="options">Options to use for reading</param>
    /// <param name="reader">Data source</param>
    /// <param name="cancellationToken">Token to cancel asynchronous enumeration</param>
    protected CsvValueEnumeratorBase(
        CsvOptions<T> options,
        ICsvBufferReader<T> reader,
        CancellationToken cancellationToken
    )
        : base(options, reader, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        Current = default!;
        _exceptionHandler = options.ExceptionHandler;
    }

    /// <summary>
    /// Returns a materializer bound to <paramref name="headers"/>.
    /// </summary>
    protected abstract IMaterializer<T, TValue> BindToHeaders(ImmutableArray<string> headers);

    /// <summary>
    /// Returns a materializer bound to field indexes.
    /// </summary>
    protected abstract IMaterializer<T, TValue> BindToHeaderless();

    /// <inheritdoc/>
    internal override bool MoveNextCore(RecordView view)
    {
        CsvReader<T> reader = _reader;
        CsvRecordRef<T> record = new(reader, ref MemoryMarshal.GetReference(_reader._buffer.Span), view);

        if (_materializer is null && InitializeMaterializerAndTryConsume(record))
        {
            return false;
        }

        try
        {
            Current = _materializer.Parse(record);
        }
        catch (CsvFormatException cfe) // unrecoverable
        {
            cfe.Enrich(Line, GetStartPosition(view), record);
            throw;
        }
        catch (Exception ex)
        {
            long position = GetStartPosition(view);
            (ex as CsvReadExceptionBase)?.Enrich(Line, position, record);
            (ex as CsvParseException)?.WithHeader(Headers);

            CsvExceptionHandler<T>? handler = _exceptionHandler;

            if (
                _exceptionHandler?.Invoke(
                    new CsvExceptionHandlerArgs<T>(
                        in record,
                        Headers,
                        ex,
                        Line,
                        position,
                        (ex as CsvReadException)?.ExpectedFieldCount
                    )
                ) == true
            )
            {
                // exception handled; try again
                return false;
            }

            throw;
        }

        return true;
    }

    /// <summary>
    /// Initializes the materializer.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the record was consumed, <c>false</c> otherwise.
    /// </returns>
    [MemberNotNull(nameof(_materializer))]
    private bool InitializeMaterializerAndTryConsume(CsvRecordRef<T> record)
    {
        if (!Options.HasHeader)
        {
            _materializer = BindToHeaderless();
            return false;
        }

        StringScratch scratch = default;
        using ValueListBuilder<string> list = new(scratch);

        Headers = CsvHeader.Parse(record);
        _materializer = BindToHeaders(Headers);
        return true;
    }
}
