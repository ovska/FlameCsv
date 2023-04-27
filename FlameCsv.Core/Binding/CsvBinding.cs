using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1036:Override methods on comparable types", Justification = "<Pending>")]
public abstract class CsvBinding : IComparable<CsvBinding>
{
    /// <summary>
    /// The CSV field index of this binding.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Returns whether the binding is on an ignored field.
    /// </summary>
    public bool IsIgnored => ReferenceEquals(Sentinel, Type.Missing);

    /// <summary>
    /// The target of the binding (member or parameter), or <see cref="Type.Missing"/> if ignored field.
    /// </summary>
    protected abstract object Sentinel { get; }

    internal protected CsvBinding(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
    }

    /// <summary>
    /// Returns a binding that ignores the field at <paramref name="index"/>.
    /// </summary>
    public static CsvBinding<T> Ignore<T>(int index)
    {
        CsvBinding<T>.ThrowIfInvalid();
        return new IgnoredCsvBinding<T>(index);
    }

    /// <summary>
    /// Returns a binding for the specified member.
    /// </summary>
    public static CsvBinding<T> For<T>(int index, Expression<Func<T, object?>> memberExpression)
    {
        ArgumentNullException.ThrowIfNull(memberExpression);
        CsvBinding<T>.ThrowIfInvalid();

        return ForMember<T>(index, memberExpression.GetMemberInfo());
    }

    /// <summary>
    /// Returns a binding for the specified member.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="CsvBindingException{T}"></exception>
    public static CsvBinding<T> ForMember<T>(int index, MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        CsvBinding<T>.ThrowIfInvalid();

        foreach (var data in CsvTypeInfo<T>.Instance.Members)
        {
            if (ReferenceEquals(data.Value, member) || AreSameMember(data.Value, member))
            {
                return new MemberCsvBinding<T>(index, data);
            }
        }

        throw new CsvBindingException<T>($"Member {member} is not applicable for type {typeof(T)}");
    }

    /// <summary>
    /// Returns a binding for the specified parameter.
    /// </summary>
    /// <exception cref="CsvBindingException{T}"></exception>
    public static CsvBinding<T> ForParameter<T>(int index, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        CsvBinding<T>.ThrowIfInvalid();

        foreach (var data in CsvTypeInfo<T>.Instance.ConstructorParameters)
        {
            if (parameter.Equals(data.Value))
                return new ParameterCsvBinding<T>(index, data);
        }

        throw new CsvBindingException<T>(
            $"Parameter {parameter} was not found on the primary constructor of {typeof(T)}");
    }

    /// <summary>
    /// Returns a binding targeting the specified header binding match.
    /// </summary>
    internal static CsvBinding<T> FromHeaderBinding<T>(object target, int index)
    {
        CsvBinding<T>.ThrowIfInvalid();

        return target switch
        {
            MemberInfo m => ForMember<T>(index, m),
            ParameterInfo p => ForParameter<T>(index, p),
            _ => ThrowHelper.ThrowInvalidOperationException<CsvBinding<T>>("Invalid HeaderBindingArgs"),
        };
    }

    /// <summary>
    /// Returns whether the binding targets the same property/field/parameter,
    /// or both bindings are ignored fields.
    /// </summary>
    public bool TargetEquals(CsvBinding? other) => AreSame(Sentinel, other?.Sentinel);

    /// <summary>
    /// Returns true if the objects are the same instance, or both are the same member.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool AreSame(object? a, object? b)
    {
        return ReferenceEquals(a, b)
            || (a is MemberInfo ma && b is MemberInfo mb && AreSameMember(ma, mb));
    }

    /// <summary>
    /// Returns true if the objects represent the same member, even if they belong to
    /// another type in the inheritance chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AreSameMember(MemberInfo a, MemberInfo b)
    {
        return a.MetadataToken == b.MetadataToken && a.Module == b.Module;
    }

    /// <inheritdoc/>
    public int CompareTo(CsvBinding? other)
    {
        return other is null ? 1 : Index.CompareTo(other.Index);
    }
}
