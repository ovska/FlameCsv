using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

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

            // Run the validation only once before caching
            if (!arr.IsEmpty)
            {
                CsvBindingException.ThrowIfInvalid<TValue>(arr);
            }

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
        var members = typeof(TValue).GetCachedPropertiesAndFields()
            .Select(
                static member =>
                {
                    object[] attributes = member.GetCachedCustomAttributes();

                    foreach (var attribute in attributes)
                    {
                        if (attribute is IndexBindingAttribute { Index: var index })
                            return new CsvBinding(index, member);
                    }

                    return new CsvBinding?();
                });

        var typeTargetedBindings = typeof(TValue).GetCachedCustomAttributes()
            .Select(
                static attr => attr switch
                {
                    IndexBindingTargetAttribute targetAttribute => targetAttribute.GetAsBinding(typeof(TValue)),
                    IndexBindingIgnoreAttribute ignoreAttribute => CsvBinding.Ignore(ignoreAttribute.Index),
                    _ => new CsvBinding?(),
                });

        return members.Concat(typeTargetedBindings).OfType<CsvBinding>();
    }
}
