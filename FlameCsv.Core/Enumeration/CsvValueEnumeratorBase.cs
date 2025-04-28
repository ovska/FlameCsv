using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Reading;
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
    : CsvEnumeratorBase<T>, IEnumerator<TValue>, IAsyncEnumerator<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Value parsed from the current CSV record.
    /// </summary>
    public TValue Current { get; private set; }

    object? IEnumerator.Current => Current;
    void IEnumerator.Reset() => ResetCore();

    /// <summary>
    /// Delegate that is called when an exception is thrown while parsing class records.
    /// If the delegate returns <see langword="true"/>, the faulty record is skipped.
    /// </summary>
    /// <remarks>
    /// <see cref="CsvFormatException"/> is not handled as it represents structurally invalid CSV.
    /// </remarks>
    public CsvExceptionHandler<T>? ExceptionHandler { get; init; }

    private IMaterializer<T, TValue>? _materializer;

    /// <summary>
    /// Returns the headers in the CSV. If headers have not been read, or <see cref="CsvOptions{T}.HasHeader"/>
    /// is <c>false</c>, returns <see langword="default"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Value is set when <see cref="CsvOptions{T}.HasHeader"/> is <c>false</c>.
    /// </exception>
    protected ImmutableArray<string> Headers
    {
        get => field;
        set
        {
            if (!Options.HasHeader) Throw.NotSupported_CsvHasNoHeader();

            if (value.IsDefaultOrEmpty || !Headers.AsSpan().SequenceEqual(value.AsSpan(), Options.Comparer))
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
        CancellationToken cancellationToken)
        : base(options, reader, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        Current = default!;
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
    protected override bool MoveNextCore(ref readonly CsvFields<T> fields)
    {
        if (_materializer is null && TryReadHeader(in fields))
        {
            return false;
        }

        try
        {
            CsvFieldsRef<T> reader = new(in fields, stackalloc T[Token<T>.StackLength]);
            Current = _materializer.Parse(ref reader);
            return true;
        }
        catch (CsvFormatException cfe) // unrecoverable
        {
            try
            {
                cfe.Line ??= Line;
                cfe.Position ??= Position;
                cfe.Record = fields.Record.Span.AsPrintableString();
            }
            catch { /* ignore */ }
            throw;
        }
        catch (Exception ex)
        {
            (ex as CsvParseException)?.Enrich(Line, Position, in fields);

            CsvExceptionHandler<T>? handler = ExceptionHandler;

            if (handler is not null &&
                handler(new CsvExceptionHandlerArgs<T>(in fields, Headers, ex, Line, Position)))
            {
                // try again
                return false;
            }

            throw;
        }
    }

    /// <summary>
    /// Initializes the materializer.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the record was consumed, <see langword="false"/> otherwise.
    /// </returns>
    [MemberNotNull(nameof(_materializer))]
    private bool TryReadHeader(ref readonly CsvFields<T> record)
    {
        if (!Options.HasHeader)
        {
            _materializer = BindToHeaderless();
            return false;
        }

        StringScratch scratch = default;
        using ValueListBuilder<string> list = new(scratch);

        CsvFieldsRef<T> reader = new(in record, stackalloc T[Token<T>.StackLength]);

        Headers = CsvHeader.Parse(Options, ref reader);
        _materializer = BindToHeaders(Headers);
        return true;
    }
}
