using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using FlameCsv.Runtime;

namespace FlameCsv.Binding.Providers;

/// <summary>
/// Binds manually defined members and column indexes.
/// </summary>
/// <typeparam name="T">Parsed token type</typeparam>
/// <typeparam name="TResult"></typeparam>
public class ManualBindingProvider<T, TResult> : ICsvBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    protected List<CsvBinding> Bindings { get; } = new();

    /// <summary>
    /// Adds the property or field to the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Column index</param>
    /// <param name="memberExpression">A simple expression returning the member, ex: <c>x => x.Id</c></param>
    /// <returns>The same provider instance</returns>
    public virtual ManualBindingProvider<T, TResult> Add(int index, Expression<Func<TResult, object?>> memberExpression)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Guard.IsNotNull(memberExpression);

        return Add(index, ReflectionUtil.GetMemberFromExpression(memberExpression));
    }

    /// <summary>
    /// Adds the property or field to the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Column index</param>
    /// <param name="memberName">Name of the property or field</param>
    /// <returns>The same provider instance</returns>
    public virtual ManualBindingProvider<T, TResult> Add(int index, string memberName)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Guard.IsNotNullOrWhiteSpace(memberName);

        var member = typeof(TResult).GetProperty(memberName)
            ?? (MemberInfo?)typeof(TResult).GetField(memberName)
            ?? throw new InvalidOperationException($"Property/field \"{memberName}\" not found on type {typeof(T)}");

        return Add(index, member);
    }

    /// <summary>
    /// Adds the property or field to the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Column index</param>
    /// <param name="member">The member's <see cref="PropertyInfo"/> or <see cref="FieldInfo"/></param>
    /// <returns>The same provider instance</returns>
    public virtual ManualBindingProvider<T, TResult> Add(int index, MemberInfo member)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Guard.IsNotNull(member);

        Bindings.Add(new CsvBinding(index, member));
        return this;
    }

    public virtual bool TryGetBindings<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        if (typeof(TValue) != typeof(TResult) && !typeof(TResult).IsAssignableFrom(typeof(TValue)))
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"{GetType().ToTypeString()} is not applicable for {typeof(TValue).ToTypeString()}");
        }

        if (Bindings.Count != 0)
        {
            bindings = new(Bindings);
            return true;
        }

        bindings = default;
        return false;
    }
}
