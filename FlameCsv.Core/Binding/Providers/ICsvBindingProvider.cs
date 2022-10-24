using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Binding.Providers;

/// <summary>
/// Binds result type members to CSV columns as standalone, without needing the CSV data.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvBindingProvider<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns the row state object used for parsing.
    /// </summary>
    /// <typeparam name="TValue">Parsed value</typeparam>
    /// <returns>Whether the binding collection can be used</returns>
    /// <exception cref="Exceptions.CsvBindingException">Bindings were found but they were invalid</exception>
    bool TryGetBindings<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings);
}
