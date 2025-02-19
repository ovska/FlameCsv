#if FEATURE_PARALLEL
using JetBrains.Annotations;

namespace FlameCsv.Reading.Parallel;

public interface ICsvAsyncInvoke<T> where T : unmanaged, IBinaryInteger<T>
{
    ValueTask InvokeAsync(CsvFieldsRef<T> fields, CsvParallelState state);
}

/// <summary>
/// Interface for invoking an action on all CSV records in parallel.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public interface ICsvParallelInvoke<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Invokes this instance on the specified fields.
    /// </summary>
    /// <param name="fields">Fields of the current CSV record</param>
    /// <param name="state">State of the enumeration</param>
    void Invoke(scoped ref CsvFieldsRef<T> fields, in CsvParallelState state);
}

internal readonly struct VoidParallelInvoke<T, TInvoke>(TInvoke invoke) : ICsvParallelTryInvoke<T, object?>
    where T : unmanaged, IBinaryInteger<T>
    where TInvoke : ICsvParallelInvoke<T>
{
    public bool TryInvoke(scoped ref CsvFieldsRef<T> fields, in CsvParallelState state, out object? result)
    {
        invoke.Invoke(ref fields, state);
        result = null!;
        return true;
    }
}
#endif
