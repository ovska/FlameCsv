using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Parallel;

/// <summary>
/// Interface for providing a value selector for parallel reading.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TResult">Returned value</typeparam>
[PublicAPI]
public interface ICsvParallelTryInvoke<T, TResult> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Attempts to return <typeparamref name="TResult"/> from the fields in <paramref name="record"/>.
    /// </summary>
    /// <param name="record">Reader for the current CSV record</param>
    /// <param name="state">State of the enumeration</param>
    /// <param name="result">Result read from the record</param>
    /// <typeparam name="TRecord">Reader type</typeparam>
    /// <returns>
    /// <see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryInvoke<TRecord>(
        scoped ref TRecord record,
        in CsvParallelState state,
        [MaybeNullWhen(false)] out TResult result)
        where TRecord : ICsvFields<T>, allows ref struct;
}
