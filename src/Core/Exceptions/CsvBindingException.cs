using System.Reflection;
using FlameCsv.Binding;
using JetBrains.Annotations;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors in CSV member binding configuration, such as invalid member types or fields.
/// </summary>
[PublicAPI]
public sealed class CsvBindingException : CsvConfigurationException
{
    /// <summary>
    /// Target type of the attempted binding.
    /// </summary>
    public Type? TargetType { get; set; }

    /// <summary>
    /// Bindings that relate to the exception (if known).
    /// </summary>
    /// <remarks>
    /// Each binding contains the CSV field index, the optional header name,
    /// and a display name of the target (property, field, or parameter, or <c>"Ignored"</c> for ignored fields).
    /// </remarks>
    public IReadOnlyList<(int Index, string? Header, string Target)>? Bindings { get; set; }

    /// <summary>
    /// Headers that were used to bind the CSV.
    /// </summary>
    public IEnumerable<string>? Headers { get; set; }

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

    /// <summary>
    /// Initializes a <see cref="CsvBindingException"/> for invalid bindings.
    /// </summary>
    internal CsvBindingException(string message, IEnumerable<CsvBinding> bindings)
        : base(message)
    {
        Bindings = ToTuples(bindings);
    }

    /// <summary>
    /// Initializes a <see cref="CsvBindingException"/> for conflicting bindings.
    /// </summary>
    internal CsvBindingException(CsvBinding first, CsvBinding second)
        : base($"Conflicting bindings: {first} and {second}")
    {
        Bindings = ToTuples([first, second]);
    }

    /// <summary>
    /// Throws an exception for conflicting constructor bindings.
    /// </summary>
    public CsvBindingException(Type target, ConstructorInfo first, ConstructorInfo second)
        : base($"Multiple constructors {target}: {first} and {second}") { }

    /// <summary>
    /// Throws an exception for a required constructor parameter that didn't have a matching binding.
    /// </summary>
    internal CsvBindingException(ParameterInfo parameter, IEnumerable<CsvBinding> parameterBindings)
        : base($"Constructor parameter '{parameter.Name}' had no matching binding and has no default value.")
    {
        Bindings = ToTuples(parameterBindings);
    }

    private static (int Index, string? Header, string Target)[] ToTuples(IEnumerable<CsvBinding> bindings)
    {
        return [.. bindings.Select(static b => (b.Index, b.Header, b.DisplayName))];
    }
}
