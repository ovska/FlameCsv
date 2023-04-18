namespace FlameCsv.Configuration;

/// <summary>
/// Provides values that can be used to configure the format when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatConfiguration<T> where T : unmanaged, IEquatable<T>
{
    string? Default { get; }
    bool TryGetOverride(Type type, out string? value);
}

