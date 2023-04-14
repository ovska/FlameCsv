using System.Reflection;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Exceptions;

public class CsvBindingException : CsvConfigurationException
{
    /// <summary>
    /// Target type of the attempted binding.
    /// </summary>
    public virtual Type? TargetType { get; }

    public virtual IEnumerable<CsvBinding> Bindings => Array.Empty<CsvBinding>();

    internal protected CsvBindingException(Type targetType, string message) : base(message)
    {
        TargetType = targetType;
    }

    internal protected CsvBindingException(
        string? message = null, Exception? innerException = null) : base(message, innerException)
    {
    }
}

/// <summary>
/// Represents errors in CSV member binding configuration, such as invalid member types or columns.
/// </summary>
public sealed class CsvBindingException<T> : CsvBindingException
{
    /// <summary>
    /// Target type of the attempted binding.
    /// </summary>
    public override Type? TargetType => typeof(T);

    /// <summary>
    /// Possible bindings that caused the exception.
    /// </summary>
    public override IReadOnlyList<CsvBinding> Bindings { get; }

    /// <inheritdoc/>
    public CsvBindingException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Bindings = Array.Empty<CsvBinding>();
    }

    /// <summary>
    /// Initializes a <see cref="CsvBindingException"/> for invalid bindings.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="bindings"></param>
    public CsvBindingException(
        string message,
        IReadOnlyList<CsvBinding<T>> bindings)
        : base(message)
    {
        Bindings = bindings;
    }

    /// <summary>
    /// Initializes a <see cref="CsvBindingException"/> for invalid target for a binding.
    /// </summary>
    /// <param name="binding">Binding not applicable for the target</param>
    public CsvBindingException(CsvBinding<T> binding)
        : base($"{binding} cannot be used for type {typeof(T)}")
    {
        Bindings = new[] { binding };
    }

    /// <summary>
    /// Throws an exception for conflicting bindings.
    /// </summary>
    public CsvBindingException(
        CsvBinding<T> first,
        CsvBinding<T> second)
        : base($"Conflicting bindings for {typeof(T)}: {first} and {second}")
    {
        Bindings = new[] { first, second };
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
        Bindings = Array.Empty<CsvBinding>();
    }

    /// <summary>
    /// Throws an exception for multiple overrides.
    /// </summary>
    public CsvBindingException(
        CsvBinding<T> binding,
        CsvParserOverrideAttribute first,
        CsvParserOverrideAttribute second)
        : base($"Multiple parser overrides defined for {binding}: {first.GetType()} and {second.GetType()}")
    {
        Bindings = new[] { binding };
    }

    /// <summary>
    /// Throws an exception for a required constructor parameter that didn't have a matching binding.
    /// </summary>
    internal CsvBindingException(ParameterInfo parameter, IEnumerable<CsvBinding> parameterBindings)
        : base($"Constructor parameter '{parameter.Name}' had no matching binding and has no default value.")
    {
        Bindings = parameterBindings.ToArray();
    }
}
