using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
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

    internal static ParameterInfo[] FindConstructorParameters<TValue>()
    {
        var ctors = typeof(TValue).GetCachedConstructors();

        if (ctors.Length == 0)
        {
            return Array.Empty<ParameterInfo>();
        }
        else if (ctors.Length == 1)
        {
            return ctors[0].GetCachedParameters();
        }

        ConstructorInfo? parameterlessCtor = null;

        foreach (var ctor in ctors)
        {
            var parameters = ctor.GetCachedParameters();

            if (ctor.HasAttribute<CsvConstuctorAttribute>())
                return parameters;

            if (ctor.GetCachedParameters().Length == 0)
                parameterlessCtor = ctor;
        }

        // No explicit ctor found, but found parameterless
        if (parameterlessCtor is not null)
        {
            return Array.Empty<ParameterInfo>();
        }

        throw new CsvBindingException(
            $"No [CsvConstructor] or empty constructor found for type {typeof(TValue)}");
    }

    internal static IEnumerable<CsvBinding> GetConstructorBindings<TValue>()
    {
        foreach (var parameter in FindConstructorParameters<TValue>())
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

            if (!found)
            {
                // TODO: unify with CsvBindingCollection
                if (!parameter.HasDefaultValue)
                    throw new CsvBindingException(
                        $"Constructor parameter '{parameter.Name}' has no index binding and no default value.");
            }
        }
    }
}
