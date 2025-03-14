﻿using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvValueEnumeratorBase{T,TValue}"/>
[PublicAPI]
[RDC(Messages.DynamicCode), RUF(Messages.Reflection)]
public sealed class CsvValueAsyncEnumerator<T, TValue> : CsvValueAsyncEnumeratorBase<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    internal CsvValueAsyncEnumerator(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken)
        : base(options, reader, cancellationToken)
    {
    }

    /// <inheritdoc />
    protected override IMaterializer<T, TValue> BindToHeaders(ReadOnlySpan<string> headers)
    {
        return _parser.Options.TypeBinder.GetMaterializer<TValue>(headers);
    }

    /// <inheritdoc />
    protected override IMaterializer<T, TValue> BindToHeaderless()
    {
        return _parser.Options.TypeBinder.GetMaterializer<TValue>();
    }
}
