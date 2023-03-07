using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors in CSV member binding configuration, such as invalid member types or columns.
/// </summary>
public sealed class CsvBindingException : CsvConfigurationException
{
    /// <summary>
    /// Target type of the attempted binding.
    /// </summary>
    public Type? TargetType { get; init; }

    /// <summary>
    /// Possible bindings that caused the exception.
    /// </summary>
    public IReadOnlyList<CsvBinding>? Bindings { get; }

    /// <inheritdoc/>
    public CsvBindingException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a <see cref="CsvBindingException"/> for invalid bindings.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="bindings"></param>
    public CsvBindingException(
        string message,
        IReadOnlyList<CsvBinding> bindings)
        : base(message)
    {
        Bindings = bindings;
    }

    /// <summary>
    /// Initializes a <see cref="CsvBindingException"/> for invalid target for a binding.
    /// </summary>
    /// <param name="target">Target type</param>
    /// <param name="binding">Binding not applicable for the target</param>
    public CsvBindingException(Type target, CsvBinding binding)
        : base($"{binding} cannot be used for type {target}")
    {
        Bindings = new[] { binding };
        TargetType = target;
    }

    /// <summary>
    /// Throws an exception for conflicting bindings.
    /// </summary>
    public CsvBindingException(
        Type target,
        CsvBinding first,
        CsvBinding second)
        : base($"Conflicting bindings for type {target}: {first} and {second}")
    {
        Bindings = new[] { first, second };
        TargetType = target;
    }

    /// <summary>
    /// Throws an exception for conflicting constructor bindings.
    /// </summary>
    public CsvBindingException(
        Type target,
        ConstructorInfo first,
        ConstructorInfo second)
        : base($"Multiple constructors {target}: {first} and {second}")
    {
        TargetType = target;
    }

    /// <summary>
    /// Throws an exception for multiple overrides.
    /// </summary>
    public CsvBindingException(
        Type target,
        CsvBinding binding,
        CsvParserOverrideAttribute first,
        CsvParserOverrideAttribute second)
        : base($"Multiple parser overrides defined for {binding}: {first.GetType()} and {second.GetType()}")
    {
        Bindings = new[] { binding };
        TargetType = target;
    }
}
