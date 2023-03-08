using System.Diagnostics;
using System.Reflection;

namespace FlameCsv.Binding;

/// <summary>
/// Parameters representing a not-yet-resolved binding passed to <see cref="CsvHeaderMatcher{T}"/>
/// to determine if the CSV column matches the potential binding.
/// </summary>
/// <remarks>
/// The built-in implementation simply matches <see cref="Value"/> to the CSV column using
/// <see cref="StringComparison.OrdinalIgnoreCase"/>.
/// </remarks>
/// <seealso cref="IHeaderBinder{T}"/>
/// <seealso cref="HeaderTextBinder"/>
/// <seealso cref="HeaderUtf8Binder"/>
public readonly struct HeaderBindingArgs
{
    /// <summary>
    /// Column index in the binding attempt.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Binding candidate value. May be from member name, custom attribute, or otherwise.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Target <see cref="MemberInfo"/> or <see cref="ParameterInfo"/>.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Order defined for the explicit binding. Default is 0.
    /// </summary>
    public int Order { get; }

    public HeaderBindingArgs(int index, string value, MemberInfo target, int order)
    {
        Index = index;
        Value = value;
        Target = target;
        Order = order;
    }

    public HeaderBindingArgs(int index, string value, ParameterInfo target, int order)
    {
        Index = index;
        Value = value;
        Target = target;
        Order = order;
    }

    public HeaderBindingArgs(int index, string value, object target, int order)
    {
        Debug.Assert(target is MemberInfo or ParameterInfo);
        Index = index;
        Value = value;
        Target = target;
        Order = order;
    }
}
