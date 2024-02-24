using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

public abstract class CsvBinding : IComparable<CsvBinding>
{
    /// <summary>
    /// The CSV field index of this binding.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The CSV field header of this binding (optional).
    /// </summary>
    public string? Header { get; }

    /// <summary>
    /// Returns whether the binding is on an ignored field.
    /// </summary>
    public bool IsIgnored => ReferenceEquals(Sentinel, typeof(CsvIgnored));

    /// <summary>
    /// The target of the binding (member or parameter), or
    /// <see langword="typeof"/> <see cref="CsvIgnored"/> if ignored field.
    /// </summary>
    internal protected abstract object Sentinel { get; }

    internal protected CsvBinding(int index, string? header)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
        Header = header;
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
    public static CsvBinding<T> For<[DynamicallyAccessedMembers(Messages.ReflectionBound)] T>(int index, Expression<Func<T, object?>> memberExpression)
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
    public static CsvBinding<T> ForMember<[DynamicallyAccessedMembers(Messages.ReflectionBound)] T>(
        int index,
        MemberInfo member,
        string? header = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        CsvBinding<T>.ThrowIfInvalid();

        foreach (var data in CsvTypeInfo<T>.Instance.Members)
        {
            if (ReferenceEquals(data.Value, member) || AreSameMember(data.Value, member))
            {
                return new MemberCsvBinding<T>(index, data, header ?? data.Value.Name);
            }
        }

        throw new CsvBindingException<T>($"Member {member} is not applicable for type {typeof(T)}");
    }

    /// <summary>
    /// Returns a binding for the specified parameter.
    /// </summary>
    /// <exception cref="CsvBindingException{T}"></exception>
    public static CsvBinding<T> ForParameter<[DynamicallyAccessedMembers(Messages.ReflectionBound)] T>(int index, ParameterInfo parameter)
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
    internal static CsvBinding<T> FromHeaderBinding<[DynamicallyAccessedMembers(Messages.ReflectionBound)] T>(
        int index,
        in HeaderBindingCandidate candidate)
    {
        CsvBinding<T>.ThrowIfInvalid();

        return candidate.Target switch
        {
            MemberInfo m => ForMember<T>(index, m, candidate.Value),
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
        return ReferenceEquals(a, b) || AreSameMember(a, b);
    }

    /// <summary>
    /// Returns true if the objects represent the same member, even if they belong to
    /// another type in the inheritance chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AreSameMember(object? a, object? b)
    {
        return a is MemberInfo ma
            && b is MemberInfo mb
            && AreSameMember(ma, mb);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreSameMember(MemberInfo ma, MemberInfo mb)
    {
        return ma.MetadataToken == mb.MetadataToken && ma.Module == mb.Module;
    }


    /// <inheritdoc/>
    public int CompareTo(CsvBinding? other)
    {
        return other is null ? 1 : Index.CompareTo(other.Index);
    }
}
