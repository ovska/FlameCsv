using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Binding;

internal static class IndexAttributeBinder
{
    private static readonly ConditionalWeakTable<Type, object?> _bindingCache = new();

    public static bool TryGet<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        if (!_bindingCache.TryGetValue(typeof(TValue), out var obj))
        {
            var list = GetBindings<TValue>().ToList();

            if (list.Count > 0)
            {
                // ensure we don't cache an invalid bindings list
                CsvBindingException.InternalThrowIfInvalid<TValue>(list);
                obj = new CsvBindingCollection<TValue>(list.ToImmutableArray());
            }
            else
            {
                obj = null;
            }

            _bindingCache.AddOrUpdate(typeof(TValue), obj);
        }

        return (bindings = obj as CsvBindingCollection<TValue>) is not null;
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
                        if (attribute is CsvIndexAttribute { Index: var index })
                            return new CsvBinding(index, member);
                    }

                    return new CsvBinding?();
                });

        var typeTargetedBindings = typeof(TValue).GetCachedCustomAttributes()
            .Select(
                static attr => attr switch
                {
                    CsvIndexTargetAttribute targetAttribute => targetAttribute.GetAsBinding(typeof(TValue)),
                    CsvIndexIgnoreAttribute ignoreAttribute => CsvBinding.Ignore(ignoreAttribute.Index),
                    _ => new CsvBinding?(),
                });

        return members.Concat(typeTargetedBindings).OfType<CsvBinding>();
    }
}
