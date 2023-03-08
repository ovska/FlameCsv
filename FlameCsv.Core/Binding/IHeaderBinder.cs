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
    /// <param name="line">CSV header record</param>
    /// <param name="options">Options of the current reader</param>
    /// <typeparam name="TValue">Value being bound</typeparam>
    /// <returns>Validated bindings</returns>
    CsvBindingCollection<TValue> Bind<TValue>(
        ReadOnlySpan<T> line,
        CsvReaderOptions<T> options);
}
