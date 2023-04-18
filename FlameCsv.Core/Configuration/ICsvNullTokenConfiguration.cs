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

/// <summary>
/// Provides values that can be used to configure the format provider used when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatProviderConfiguration<T> where T : unmanaged, IEquatable<T>
{
    bool TryGetOverride(Type type, out IFormatProvider? value);
    IFormatProvider? Default { get; }
}

/// <summary>
/// Provides values that can be used to configure the format when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatConfiguration<T> where T : unmanaged, IEquatable<T>
{
    string? Default { get; }
    bool TryGetOverride(Type type, out string? value);
}

