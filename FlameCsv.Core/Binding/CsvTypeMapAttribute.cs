﻿namespace FlameCsv.Binding;

/// <summary>
/// Applies source generated binding logic to the annotated partial class.
/// </summary>
/// <remarks>
/// <see cref="CsvTypeMap{T, TValue}"/> instances should be stateless and immutable.
/// <br/>To use custom value creation logic, create a parameterless instance or static method <c>CreateInstance()</c>
/// that returns <typeparamref name="TValue"/> or a subtype.
/// </remarks>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TValue"></typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CsvTypeMapAttribute<T, TValue> : Attribute where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// If <see langword="true"/>, headers that cannot be matched to a member are ignored instead of throwing.
    /// </summary>
    public bool IgnoreUnmatched { get; set; }

    /// <summary>
    /// If <see langword="true"/>, records that have unparsable values are ignored instead of throwing.
    /// </summary>
    public bool IgnoreUnparsable { get; set; }

    /// <summary>
    /// If <see langword="true"/>, multiple header field matches to a single member throw an exception.
    /// The default behavior does not attempt to match already matched members.
    /// </summary>
    public bool ThrowOnDuplicate { get; set; }

    /// <summary>
    /// Don't create a static <c>Instance</c>-property to avoid allocations when reusing the type map.
    /// </summary>
    public bool SkipStaticInstance { get; set; }
}