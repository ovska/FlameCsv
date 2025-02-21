using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using System.Runtime.CompilerServices;
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

    private readonly bool _hasHeader;
    private readonly bool _hasCallback;

    private IMaterializer<T, TValue>? _materializer;
    private string[]? _headersArray;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvValueEnumeratorBase{T, TValue}"/>.
    /// </summary>
    /// <param name="options">Options to use for reading</param>
    /// <param name="reader">Data source</param>
    /// <param name="cancellationToken">Token to cancel asynchronous enumeration</param>
    protected CsvValueEnumeratorBase(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken)
        : base(options, reader, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        _hasCallback = options.RecordCallback is not null;
        _hasHeader = options.HasHeader;
        Current = default!;
    }

    /// <summary>
    /// Returns a materializer bound to <paramref name="headers"/>.
    /// </summary>
    protected abstract IMaterializer<T, TValue> BindToHeaders(ReadOnlySpan<string> headers);

    /// <summary>
    /// Returns a materializer bound to field indexes.
    /// </summary>
    protected abstract IMaterializer<T, TValue> BindToHeaderless();

    /// <inheritdoc/>
    protected override ReadOnlySpan<string> GetHeader() => _headersArray;

    /// <inheritdoc/>
    protected override void ResetHeader()
    {
        _materializer = null;
        _headersArray = null;
    }

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
        catch (Exception ex)
        {
            // this is treated as an unrecoverable exception
            if (ex is CsvFormatException)
            {
                ThrowInvalidFormatException(ex, in fields);
            }

            var handler = ExceptionHandler;

            if (handler is not null)
            {
                CsvExceptionHandlerArgs<T> args = new(in fields, _headersArray, ex, Line, Position);

                if (handler(in args))
                {
                    // try again
                    return false;
                }
            }

            EnrichParseException(ex as CsvParseException, in fields);
            ThrowUnhandledException(ex, in fields);
            return false; // unreachable
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
        if (!_hasHeader)
        {
            _materializer = BindToHeaderless();
            return false;
        }

        StringScratch scratch = default;
        using ValueListBuilder<string> list = new(scratch);

        CsvFieldsRef<T> reader = new(in record, stackalloc T[Token<T>.StackLength]);

        _materializer = CsvHeader.Parse(
            Parser.Options,
            ref reader,
            this,
            static (@this, headers) =>
            {
                IMaterializer<T, TValue> materializer = @this.BindToHeaders(headers);

                if (@this._hasCallback || @this.ExceptionHandler is not null)
                {
                    @this._headersArray = headers.ToArray();
                }

                return materializer;
            });

        return true;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnhandledException(Exception innerException, in CsvFields<T> fields)
    {
        string message = $"Unhandled exception while reading records of type {typeof(TValue).FullName} from the CSV.";

        // HACK: don't include the record if the exception is already enriched
        if (innerException is not CsvParseException { AdditionalMessage: not null })
        {
            message
                += $" The record was at position {Position} on line {Line} in the CSV. Record: {fields.Data.Span.AsPrintableString()}";
        }

        throw new CsvUnhandledException(message, Line, Position, innerException);
    }
}
