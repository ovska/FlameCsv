﻿using System.ComponentModel;
using FlameCsv.Binding;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Reads <typeparamref name="TValue"/> records from CSV.
/// </summary>
[PublicAPI]
public sealed class CsvTypeMapAsyncEnumerable<T, TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvTypeMap<T, TValue> _typeMap;
    private CsvExceptionHandler<T>? _exceptionHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvTypeMapAsyncEnumerable{T,TValue}"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public CsvTypeMapAsyncEnumerable(
        ICsvPipeReader<T> reader,
        CsvOptions<T> options,
        CsvTypeMap<T, TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        _options = options;
        _reader = reader;
        _typeMap = typeMap;
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvTypeMapAsyncEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvTypeMapAsyncEnumerator<T, TValue>(_options, _typeMap, _reader, cancellationToken)
        {
            ExceptionHandler = _exceptionHandler
        };
    }

    [MustDisposeResource]
    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }

    /// <inheritdoc cref="CsvValueEnumerable{T,TValue}.WithExceptionHandler"/>
    public CsvTypeMapAsyncEnumerable<T, TValue> WithExceptionHandler(CsvExceptionHandler<T>? handler)
    {
        _exceptionHandler = handler;
        return this;
    }
}
