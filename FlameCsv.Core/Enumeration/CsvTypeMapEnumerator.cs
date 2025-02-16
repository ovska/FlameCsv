using System.Buffers;
using System.Collections;
using FlameCsv.Binding;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvValueAsyncEnumeratorBase{T,TValue}"/>
[PublicAPI]
public sealed class CsvTypeMapEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>, IEnumerator<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvTypeMap<T, TValue> _typeMap;
    private readonly ReadOnlySequence<T> _csv;

    internal CsvTypeMapEnumerator(in ReadOnlySequence<T> csv, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        _typeMap = typeMap;
        _csv = csv;
        _parser.SetData(in csv);
    }

    /// <inheritdoc cref="IEnumerator.MoveNext"/>
    public bool MoveNext()
    {
        return TryRead(isFinalBlock: false) || TryRead(isFinalBlock: true);
    }

    // RIDER complains about this class otherwise
    /// <inheritdoc cref="IEnumerator{T}.Current"/>
    public new TValue Current => base.Current;

    /// <inheritdoc/>
    protected override IMaterializer<T, TValue> BindToHeaders(ReadOnlySpan<string> headers)
    {
        return _typeMap.GetMaterializer(headers, _parser.Options);
    }

    /// <inheritdoc/>
    protected override IMaterializer<T, TValue> BindToHeaderless()
    {
        return _typeMap.GetMaterializer(_parser.Options);
    }

    object IEnumerator.Current => Current!;

    /// <inheritdoc />
    public void Reset() => _parser.SetData(in _csv);
}
