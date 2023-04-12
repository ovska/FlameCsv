using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Configuration;



/// <summary>
/// Provides values that can be used to configure the null token used when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvNullTokenConfiguration<T>    where T : unmanaged, IEquatable<T>
{
    ReadOnlyMemory<T> GetNullToken(Type type);
}

/// <summary>
/// Provides values that can be used to configure the format provider used when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatProviderConfiguration<T>     where T : unmanaged, IEquatable<T>
{
    IFormatProvider? GetFormatProvider(Type type);
}

/// <summary>
/// Provides values that can be used to configure the format when parsing and formatting supported types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatConfiguration<T>    where T : unmanaged, IEquatable<T>
{
    string? GetFormat(Type type);
}

