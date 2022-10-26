using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding.Providers;

/// <summary>
/// Binds or ignores indexes to members via <see cref="IndexBindingAttribute"/>,
/// <see cref="IndexBindingTargetAttribute"/> and <see cref="IndexBindingIgnoreAttribute"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class IndexBindingProvider<T> : ICsvBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, Tuple<ImmutableArray<CsvBinding>>> _bindings = new();

    public bool TryGetBindings<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        if (!_bindings.TryGetValue(typeof(TValue), out var tuple))
        {
            var arr = GetBindings<TValue>().OrderBy(b => b.Index).ToImmutableArray();
            CsvBindingException.ThrowIfInvalid<TValue>(arr);
            _bindings.AddOrUpdate(typeof(TValue), tuple = new(arr));
        }

        if (!tuple.Item1.IsEmpty)
        {
            bindings = new CsvBindingCollection<TValue>(tuple.Item1);
            return true;
        }

        bindings = null;
        return false;
    }

    private static IEnumerable<CsvBinding> GetBindings<TValue>()
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

        return members.Concat(targeted).Concat(ignored);
    }
}
