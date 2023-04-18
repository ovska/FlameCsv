namespace FlameCsv.Configuration;

/// <summary>
/// Provides values that can be used to configure the format provider used when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatProviderConfiguration<T> where T : unmanaged, IEquatable<T>
{
    bool TryGetOverride(Type type, out IFormatProvider? value);
    IFormatProvider? Default { get; }
}

