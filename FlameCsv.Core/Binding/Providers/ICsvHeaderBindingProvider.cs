namespace FlameCsv.Binding.Providers;

/// <summary>
/// Binds result type members to CSV columns using the header.
/// </summary>
public interface ICsvHeaderBindingProvider<T> : ICsvBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Attempts to parse bindings from the line.
    /// </summary>
    /// <param name="line"></param>
    /// <param name="configuration"></param>
    /// <returns>The header was processed and the bindings can be used.</returns>
    bool TryProcessHeader(ReadOnlySpan<T> line, CsvConfiguration<T> configuration);
}
