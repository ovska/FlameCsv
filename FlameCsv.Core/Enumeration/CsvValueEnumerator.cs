using System.Buffers;
using System.Collections;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvValueAsyncEnumeratorBase{T,TValue}"/>
[PublicAPI]
[RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
public sealed class CsvValueEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>, IEnumerator<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySequence<T> _csv;

    internal CsvValueEnumerator(in ReadOnlySequence<T> csv, CsvOptions<T> options) : base(options)
    {
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
        return _parser.Options.TypeBinder.GetMaterializer<TValue>(headers);
    }

    /// <inheritdoc/>
    protected override IMaterializer<T, TValue> BindToHeaderless()
    {
        return _parser.Options.TypeBinder.GetMaterializer<TValue>();
    }

    object IEnumerator.Current => Current!;

    /// <inheritdoc />
    public void Reset() => _parser.SetData(in _csv);
}
