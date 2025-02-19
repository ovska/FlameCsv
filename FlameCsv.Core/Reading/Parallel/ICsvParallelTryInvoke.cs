#if FEATURE_PARALLEL
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace FlameCsv.Reading.Parallel;

/// <summary>
/// Interface for providing a value selector for parallel reading.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TResult">Returned value</typeparam>
[PublicAPI]
public interface ICsvParallelTryInvoke<T, TResult> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Attempts to return <typeparamref name="TResult"/> from the record.
    /// </summary>
    /// <param name="fields">Fields of the current CSV record</param>
    /// <param name="state">State of the enumeration</param>
    /// <param name="result">Result read from the record</param>
    /// <returns>
    /// <see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryInvoke(
        scoped ref CsvFieldsRef<T> fields,
        in CsvParallelState state,
        [MaybeNullWhen(false)] out TResult result);
}
#endif
