using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Binding.Providers;

/// <summary>
/// Binds or ignores indexes to members via <see cref="IndexBindingAttribute"/>,
/// <see cref="IndexBindingTargetAttribute"/> and <see cref="IndexBindingIgnoreAttribute"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class IndexBindingProvider<T> : ICsvBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    public bool TryGetBindings<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        var members = typeof(TValue).GetMembers(CsvBindingConstants.MemberLookupFlags)
            .Where(static m => m is PropertyInfo or FieldInfo)
            .Select(
                static member => member.GetCustomAttribute<IndexBindingAttribute>() is { Index: var index }
                    ? new CsvBinding(index, member)
                    : new CsvBinding?())
            .OfType<CsvBinding>();

        var targeted = typeof(TValue).GetCustomAttributes<IndexBindingTargetAttribute>()
            .Select(static attr => attr.GetAsBinding(typeof(TValue)));

        var ignored = typeof(TValue).GetCustomAttributes<IndexBindingIgnoreAttribute>()
            .Select(static attr => CsvBinding.Ignore(attr.Index));

        var foundBindings = members.Concat(targeted).Concat(ignored).ToList();

        if (foundBindings.Count == 0)
        {
            bindings = default;
            return false;
        }

        bindings = new CsvBindingCollection<TValue>(foundBindings);
        return true;
    }
}
