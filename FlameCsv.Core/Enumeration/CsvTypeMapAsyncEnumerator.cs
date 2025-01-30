using FlameCsv.Binding;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvValueEnumeratorBase{T,TValue}"/>
[PublicAPI]
public sealed class CsvTypeMapAsyncEnumerator<T, TValue> : CsvValueAsyncEnumeratorBase<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvTypeMap<T, TValue> _typeMap;

    internal CsvTypeMapAsyncEnumerator(
        CsvOptions<T> options,
        CsvTypeMap<T, TValue> typeMap,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken)
        : base(options, reader, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        _typeMap = typeMap;
    }

    /// <inheritdoc />
    protected override IMaterializer<T, TValue> BindToHeaders(ReadOnlySpan<string> headers)
    {
        return _typeMap.GetMaterializer(headers, _parser.Options);
    }

    /// <inheritdoc />
    protected override IMaterializer<T, TValue> BindToHeaderless()
    {
        return _typeMap.GetMaterializer(_parser.Options);
    }
}
