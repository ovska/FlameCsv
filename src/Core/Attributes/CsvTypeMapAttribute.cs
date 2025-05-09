using FlameCsv.Binding;
using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Applies source generated binding logic to the annotated partial class.
/// </summary>
/// <remarks>
/// <see cref="CsvTypeMap{T,TValue}"/> instances should be stateless and immutable.
/// <br/>To use custom value creation logic, create a parameterless instance or static method <c>CreateInstance()</c>
/// that returns <typeparamref name="TValue"/> or a subtype.
/// <br/>Automatically creates a <c>Instance</c>-property if the type has a parameterless constructor,
/// and no member with that name already exists.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
[PublicAPI]
public sealed class CsvTypeMapAttribute<T, TValue> : Attribute
    where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="CsvTypeMap.IgnoreUnmatched"/>
    public bool IgnoreUnmatched { get; set; }

    /// <inheritdoc cref="CsvTypeMap.ThrowOnDuplicate"/>
    public bool ThrowOnDuplicate { get; set; }

    /// <inheritdoc cref="CsvTypeMap.NoCaching"/>
    public bool NoCaching { get; set; }

    /// <summary>
    /// If <c>true</c>, the source generator will scan for attributes applied to the containing assembly.
    /// </summary>
    public bool SupportsAssemblyAttributes { get; set; }
}
