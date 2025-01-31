using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the constructor to be used when creating instances while reading records.
/// If omitted and the type has only one public constructor, that constructor will be used.
/// Otherwise, an empty constructor will be used if one is available.
/// </summary>
/// <remarks>
/// This attribute is only used when reading CSV.
/// </remarks>
[PublicAPI]
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class CsvConstructorAttribute : Attribute;
