using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Generates a type converter for the enum type.
/// </summary>
/// <typeparam name="T">Token type (<see langword="char"/> or <see langword="byte"/>)</typeparam>
/// <typeparam name="TEnum">Enum type</typeparam>
/// <remarks>
/// This feature is in preview and may be subject to change in future versions.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
[PublicAPI]
public class CsvEnumConverterAttribute<T, TEnum> : Attribute
    where T : unmanaged, IBinaryInteger<T>
    where TEnum : struct, Enum;
