using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvValueEnumeratorBase{T,TValue}"/>
[PublicAPI]
[RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
public sealed class CsvValueEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Initializes a new instance of <see cref="CsvValueEnumerator{T, TValue}"/>.
    /// </summary>
    /// <param name="options">Options to use for reading</param>
    /// <param name="reader">Data source</param>
    /// <param name="cancellationToken">Token to cancel asynchronous enumeration</param>
    public CsvValueEnumerator(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken = default)
        : base(options, reader, cancellationToken)
    {
    }

    /// <inheritdoc/>
    protected override IMaterializer<T, TValue> BindToHeaders(ReadOnlySpan<string> headers)
    {
        return Parser.Options.TypeBinder.GetMaterializer<TValue>(headers);
    }

    /// <inheritdoc/>
    protected override IMaterializer<T, TValue> BindToHeaderless()
    {
        return Parser.Options.TypeBinder.GetMaterializer<TValue>();
    }
}
