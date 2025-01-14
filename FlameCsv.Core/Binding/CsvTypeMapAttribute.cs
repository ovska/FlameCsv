﻿using JetBrains.Annotations;

namespace FlameCsv.Binding;

/// <summary>
/// Applies source generated binding logic to the annotated partial class.
/// </summary>
/// <remarks>
/// <see cref="CsvTypeMap{T, TValue}"/> instances should be stateless and immutable.
/// <br/>To use custom value creation logic, create a parameterless instance or static method <c>CreateInstance()</c>
/// that returns <typeparamref name="TValue"/> or a subtype.
/// <br/>Automatically creates a <c>Instance</c>-property if the type has a parameterless constructor,
/// and no member with that name already exists.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
[PublicAPI]
public sealed class CsvTypeMapAttribute<T, TValue> : Attribute where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Whether the generated code is valid for reading, writing, or both (the default).
    /// </summary>
    public CsvBindingScope Scope { get; set; }

    /// <summary>
    /// If <see langword="true"/>, headers that cannot be matched to a member are ignored instead of throwing.
    /// </summary>
    public bool IgnoreUnmatched { get; set; }

    /// <summary>
    /// If <see langword="true"/>, multiple header field matches to a single member throw an exception.
    /// The default behavior does not attempt to match already matched members.
    /// </summary>
    public bool ThrowOnDuplicate { get; set; }

    /// <summary>
    /// If <see langword="true"/> (the default), built-in converters are used for <see cref="Enum"/>
    /// and <see cref="Nullable{T}"/> unless specified on the member explicitly.
    /// </summary>
    public bool UseBuiltinConverters { get; set; } = true;
}
