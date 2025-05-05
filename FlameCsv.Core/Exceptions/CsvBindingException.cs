using System.Reflection;
using FlameCsv.Binding;
using JetBrains.Annotations;

namespace FlameCsv.Exceptions;

/// <inheritdoc cref="CsvBindingException{T}"/>
[PublicAPI]
public class CsvBindingException : CsvConfigurationException
{
    /// <summary>
    /// Target type of the attempted binding.
    /// </summary>
    public virtual Type? TargetType { get; }

    /// <summary>
    /// Bindings that relate to the exception (if known).
    /// </summary>
    public virtual IReadOnlyList<CsvBinding> Bindings => [];

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public CsvBindingException(Type? targetType, string message)
        : base(message)
    {
        TargetType = targetType;
    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public CsvBindingException(string? message = null, Exception? innerException = null)
        : base(message, innerException) { }
}

/// <summary>
/// Represents errors in CSV member binding configuration, such as invalid member types or fields.
/// </summary>
public sealed class CsvBindingException<T> : CsvBindingException
{
    /// <summary>
    /// Target type of the attempted binding.
    /// </summary>
    public override Type TargetType => typeof(T);

    /// <summary>
    /// Possible bindings that caused the exception.
    /// </summary>
    public override IReadOnlyList<CsvBinding> Bindings { get; }

    /// <inheritdoc/>
    public CsvBindingException(string? message = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Bindings = [];
    }

    /// <summary>
    /// Initializes a <see cref="CsvBindingException"/> for invalid bindings.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="bindings"></param>
    public CsvBindingException(string message, IReadOnlyList<CsvBinding<T>> bindings)
        : base(message)
    {
        Bindings = bindings;
    }

    /// <summary>
    /// Throws an exception for conflicting bindings.
    /// </summary>
    public CsvBindingException(CsvBinding<T> first, CsvBinding<T> second)
        : base($"Conflicting bindings for {typeof(T)}: {first} and {second}")
    {
        Bindings = [first, second];
    }

    /// <summary>
    /// Throws an exception for conflicting constructor bindings.
    /// </summary>
    public CsvBindingException(Type target, ConstructorInfo first, ConstructorInfo second)
        : base($"Multiple constructors {target}: {first} and {second}")
    {
        Bindings = [];
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
