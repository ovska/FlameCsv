using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Value read</typeparam>
[PublicAPI]
public interface ICsvValueAsyncEnumerable<T, out TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="CsvValueEnumerable{T,TValue}.WithExceptionHandler"/>
    ICsvValueAsyncEnumerable<T, TValue> WithExceptionHandler(CsvExceptionHandler<T>? handler);
}
