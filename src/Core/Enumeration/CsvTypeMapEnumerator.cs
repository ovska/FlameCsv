using System.Collections.Immutable;
using FlameCsv.Binding;
using FlameCsv.IO;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvValueEnumeratorBase{T,TValue}"/>
[PublicAPI]
public sealed class CsvTypeMapEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvTypeMap<T, TValue> _typeMap;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvValueEnumerator{T, TValue}"/>.
    /// </summary>
    /// <param name="options">Options to use for reading</param>
    /// <param name="typeMap">Type map used for binding</param>
    /// <param name="reader">Data source</param>
    /// <param name="cancellationToken">Token to cancel asynchronous enumeration</param>
    public CsvTypeMapEnumerator(
        CsvOptions<T> options,
        CsvTypeMap<T, TValue> typeMap,
        ICsvBufferReader<T> reader,
        CancellationToken cancellationToken = default
    )
        : base(options, reader, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        _typeMap = typeMap;
    }

    /// <inheritdoc/>
    protected override IMaterializer<T, TValue> BindToHeaders(ImmutableArray<string> headers)
    {
        return _typeMap.GetMaterializer(headers, Options);
    }

    /// <inheritdoc/>
    protected override IMaterializer<T, TValue> BindToHeaderless()
    {
        return _typeMap.GetMaterializer(Options);
    }
}
