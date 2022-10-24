using System.Reflection;

namespace FlameCsv.Binding;

public readonly struct HeaderBindingArgs
{
    /// <summary>
    /// Column index in the binding attempt.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Type targeted by the binding provider.
    /// </summary>
    public Type TargetType { get; init; }

    /// <summary>
    /// Binding candidate value. May be from member name, custom attribute, or otherwise.
    /// </summary>
    public string Value { get; init; }

    /// <summary>
    /// Target member.
    /// </summary>
    public MemberInfo Member { get; init; }

    /// <summary>
    /// Order defined for the explicit binding. Default is 0.
    /// </summary>
    public int Order { get; init; }
}
