using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Parallel;

/// <summary>
/// Interface for invoking an action on all CSV records in parallel.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public interface ICsvParallelInvoke<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Applies the effects of this instance to the fields in <paramref name="record"/>.
    /// </summary>
    /// <param name="record">Reader for the current CSV record</param>
    /// <param name="state">State of the enumeration</param>
    /// <typeparam name="TRecord">Reader type</typeparam>
    void Invoke<TRecord>(scoped ref TRecord record, in CsvParallelState state)
        where TRecord : ICsvFields<T>, allows ref struct;
}

internal readonly struct VoidParallelInvoke<T, TInvoke>(TInvoke invoke) : ICsvParallelTryInvoke<T, object?>
    where T : unmanaged, IBinaryInteger<T>
    where TInvoke : ICsvParallelInvoke<T>
{
    public bool TryInvoke<TRecord>(
        scoped ref TRecord record,
        in CsvParallelState state,
        [NotNullWhen(true)] out object? result) where TRecord : ICsvFields<T>, allows ref struct
    {
        invoke.Invoke(ref record, state);
        result = null!;
        return true;
    }
}
