using System.Diagnostics.Metrics;
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
    /// The CSV column index of this binding.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Returns whether the binding is on an ignored column.
    /// </summary>
    public bool IsIgnored => ReferenceEquals(Sentinel, Type.Missing);

    protected abstract object Sentinel { get; }

    internal protected CsvBinding(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
    }

    /// <summary>Marks the column at <paramref name="index"/> as ignored.</summary>
    public static CsvBinding<T> Ignore<T>(int index)
    {
        CsvBinding<T>.ThrowIfInvalid();
        return new IgnoredCsvBinding<T>(index);
    }

    public static CsvBinding<T> For<T>(int index, Expression<Func<T, object?>> memberExpression)
    {
        CsvBinding<T>.ThrowIfInvalid();
        return ForMember<T>(index, memberExpression.GetMemberInfo());
    }

    public static CsvBinding<T> ForMember<T>(int index, MemberInfo member)
    {
        CsvBinding<T>.ThrowIfInvalid();
        foreach (var data in CsvTypeInfo<T>.Instance.Members)
        {
            if (AreSameMember(data.Value, member))
            {
                return new MemberCsvBinding<T>(index, data);
            }
        }

        throw new CsvBindingException<T>($"Member {member} is not applicable for type {typeof(T)}");
    }

    public static CsvBinding<T> ForParameter<T>(int index, ParameterInfo parameter)
    {
        CsvBinding<T>.ThrowIfInvalid();
        foreach (var data in CsvTypeInfo<T>.Instance.ConstructorParameters)
        {
            if (parameter.Equals(data.Value))
                return new ParameterCsvBinding<T>(index, data);
        }

        throw new CsvBindingException<T>(
            $"Parameter {parameter} was not found on the primary constructor of {typeof(T)}");
    }

    public static CsvBinding<T> FromHeaderBinding<T>(in HeaderBindingArgs candidate)
    {
        CsvBinding<T>.ThrowIfInvalid();
        return candidate.Target switch
        {
            MemberInfo m => ForMember<T>(candidate.Index, m),
            ParameterInfo p => ForParameter<T>(candidate.Index, p),
            _ => ThrowHelper.ThrowInvalidOperationException<CsvBinding<T>>("Invalid HeaderBindingArgs"),
        };
    }

    /// <summary>
    /// Returns whether the binding targets the same property/field/parameter,
    /// or both bindings are ignored columns.
    /// </summary>
    public bool TargetEquals(CsvBinding other) => AreSame(Sentinel, other.Sentinel);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool AreSame(object a, object b)
    {
        return a == b
            || (a is MemberInfo ma && b is MemberInfo mb && AreSameMember(ma, mb));
    }

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
