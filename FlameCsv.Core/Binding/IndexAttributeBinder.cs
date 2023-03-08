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
            var list = GetBindings<TValue>();

            if (list.Count > 0)
            {
                obj = new CsvBindingCollection<TValue>(list, isInternalCall: true);
            }
            else
            {
                obj = null;
            }

            _bindingCache.AddOrUpdate(typeof(TValue), obj);
        }

        if (obj is null)
        {
            bindings = null;
            return false;
        }

        bindings = (CsvBindingCollection<TValue>)obj;
        return true;
    }

    internal static List<CsvBinding> GetBindings<TValue>()
    {
        List<CsvBinding> list = new();

        foreach (var member in typeof(TValue).GetCachedPropertiesAndFields())
        {
            foreach (var attribute in member.GetCachedCustomAttributes())
            {
                if (attribute is CsvIndexAttribute { Index: var index })
                {
                    list.Add(CsvBinding.ForMember(index, member));
                    break;
                }
            }
        }

        foreach (var attr in typeof(TValue).GetCachedCustomAttributes())
        {
            if (attr is CsvIndexTargetAttribute targetAttribute)
            {
                list.Add(targetAttribute.GetAsBinding(typeof(TValue)));
            }
            else if (attr is CsvIndexIgnoreAttribute ignoreAttribute)
            {
                list.Add(CsvBinding.Ignore(ignoreAttribute.Index));
            }
        }

        list.AddRange(GetConstructorBindings<TValue>());

        return list;
    }

    internal static IEnumerable<CsvBinding> GetConstructorBindings<TValue>()
    {
        foreach (var parameter in ReflectionExtensions.FindConstructorParameters<TValue>())
        {
            bool found = false;

            foreach (var attr in parameter.GetCachedParameterAttributes())
            {
                if (attr is CsvIndexAttribute { Index: var index })
                {
                    yield return new CsvBinding(index, parameter);
                    found = true;
                    break;
                }
            }

            if (!found && !parameter.HasDefaultValue)
            {
                throw new CsvBindingException(typeof(TValue), parameter);
            }
        }
    }
}
