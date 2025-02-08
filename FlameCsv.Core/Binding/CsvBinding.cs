using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reflection;
using JetBrains.Annotations;

namespace FlameCsv.Binding;

/// <summary>
/// Base class representing a binding of a member or parameter to a CSV field.
/// </summary>
[PublicAPI]
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
    /// Equality contract of the binding. The target of the binding (member or parameter), or
    /// <see langword="typeof"/> <see cref="CsvIgnored"/> if ignored field.
    /// </summary>
    protected abstract object Sentinel { get; }

    internal CsvBinding(int index, string? header)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        Index = index;
        Header = header;
    }

    /// <summary>
    /// Returns a binding that ignores the field at <paramref name="index"/>.
    /// </summary>
    public static CsvBinding<T> Ignore<T>(int index)
    {
        return new IgnoredCsvBinding<T>(index);
    }

    /// <summary>
    /// Returns a binding for the specified member.
    /// </summary>
    [RDC(Messages.Reflection)]
    public static CsvBinding<T> For<[DAM(Messages.ReflectionBound)] T>(int index, Expression<Func<T, object?>> memberExpression)
    {
        ArgumentNullException.ThrowIfNull(memberExpression);
        return ForMember<T>(index, memberExpression.GetMemberInfo());
    }

    /// <summary>
    /// Returns a binding for the specified member.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="CsvBindingException{T}"></exception>
    [RDC(Messages.Reflection)]
    public static CsvBinding<T> ForMember<[DAM(Messages.ReflectionBound)] T>(
        int index,
        MemberInfo member,
        string? header = null)
    {
        ArgumentNullException.ThrowIfNull(member);

        foreach (var data in CsvTypeInfo<T>.Value.Members)
        {
            if (ReferenceEquals(data.Value, member) || AreSameMember(data.Value, member))
            {
                return new MemberCsvBinding<T>(index, data, header ?? data.Value.Name);
            }
        }

        if (CsvTypeInfo<T>.Value.Proxy is { } proxyTypeInfo)
        {
            foreach (var data in proxyTypeInfo.Members)
            {
                if (ReferenceEquals(data.Value, member) || AreSameMember(data.Value, member))
                {
                    return new MemberCsvBinding<T>(index, data, header ?? data.Value.Name);
                }
            }
        }

        throw new CsvBindingException<T>($"Member {member} is not applicable for type {typeof(T)}");
    }

    /// <summary>
    /// Returns a binding for the specified parameter.
    /// </summary>
    /// <exception cref="CsvBindingException{T}"></exception>
    public static CsvBinding<T> ForParameter<[DAM(Messages.ReflectionBound)] T>(int index, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        foreach (var data in CsvTypeInfo<T>.Value.ConstructorParameters)
        {
            if (parameter.Equals(data.Value))
                return new ParameterCsvBinding<T>(index, data);
        }

        throw new CsvBindingException<T>(
            $"Parameter {parameter} was not found on the constructor of {typeof(T)}");
    }

    /// <summary>
    /// Returns a binding targeting the specified header binding match.
    /// </summary>
    [RDC(Messages.Reflection)]
    internal static CsvBinding<T> FromBindingData<[DAM(Messages.ReflectionBound)] T>(
        int index,
        in BindingData data)
    {
        return data.Target switch
        {
            MemberInfo m => ForMember<T>(index, m, data.Name),
            ParameterInfo p => ForParameter<T>(index, p),
            _ => throw new InvalidOperationException($"Invalid {nameof(BindingData)}.Target: {data.Target}"),
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
