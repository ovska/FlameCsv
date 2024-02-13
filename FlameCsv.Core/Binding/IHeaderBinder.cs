using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Binding;

/// <summary>
/// Binds CSV header to members.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface IHeaderBinder<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns bindings parsed from the line.
    /// </summary>
    /// <param name="headerFields">CSV header record</param>
    /// <typeparam name="TValue">Value being bound</typeparam>
    /// <returns>Validated bindings</returns>
    CsvBindingCollection<TValue> Bind<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(ReadOnlySpan<string> headerFields);

    /// <summary>
    /// Returns bindings to write CSV.
    /// </summary>
    CsvBindingCollection<TValue> Bind<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>();
}
