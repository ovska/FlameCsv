using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Generates a type converter for the enum type when placed on a partial class.
/// </summary>
/// <typeparam name="T">Token type (<see langword="char"/> or <see langword="byte"/>)</typeparam>
/// <typeparam name="TEnum">Enum type</typeparam>
[AttributeUsage(AttributeTargets.Class)]
[PublicAPI]
public sealed class CsvEnumConverterAttribute<T, TEnum> : Attribute
    where T : unmanaged, IBinaryInteger<T>
    where TEnum : struct, Enum;
