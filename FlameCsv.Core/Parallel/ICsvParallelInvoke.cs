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
    /// Applies the effects of this instance to the fields in <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">Reader for the current CSV record</param>
    /// <param name="state">State of the enumeration</param>
    /// <typeparam name="TReader">Reader type</typeparam>
    void Invoke<TReader>(scoped ref TReader reader, in CsvParallelState state)
        where TReader : ICsvRecordFields<T>, allows ref struct;
}

internal readonly struct VoidParallelInvoke<T, TInvoke>(TInvoke invoke) : ICsvParallelTryInvoke<T, object?>
    where T : unmanaged, IBinaryInteger<T>
    where TInvoke : ICsvParallelInvoke<T>
{
    public bool TryInvoke<TReader>(
        scoped ref TReader reader,
        in CsvParallelState state,
        [NotNullWhen(true)] out object? result) where TReader : ICsvRecordFields<T>, allows ref struct
    {
        throw new NotImplementedException();
    }
}
