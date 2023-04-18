namespace FlameCsv.Configuration;

/// <summary>
/// Provides values that can be used to configure the null token used when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvNullTokenConfiguration<T> where T : unmanaged, IEquatable<T>
{
    bool TryGetOverride(Type type, out ReadOnlyMemory<T> value);
    ReadOnlyMemory<T> Default { get; }
}
