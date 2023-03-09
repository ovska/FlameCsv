using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Binding;

internal static class IndexAttributeBinder<TValue>
{
    private static readonly Lazy<CsvBindingCollection<TValue>?> _bindingsLazy = new(CreateBindingCollection);

    public static bool TryGetBindings([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        return (bindings = _bindingsLazy.Value) is not null;
    }

    private static CsvBindingCollection<TValue>? CreateBindingCollection()
    {
        List<CsvBinding> list = new();

        // Member attributes
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

        // Type targeted attributes
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

        // Primary constructor parameters
        foreach (var parameter in ReflectionExtensions.FindConstructorParameters<TValue>())
        {
            bool found = false;

            foreach (var attr in parameter.GetCachedParameterAttributes())
            {
                if (attr is CsvIndexAttribute { Index: var index })
                {
                    list.Add(new CsvBinding(index, parameter));
                    found = true;
                    break;
                }
            }

            if (!found && !parameter.HasDefaultValue)
            {
                throw new CsvBindingException(typeof(TValue), parameter);
            }
        }

        return list.Count > 0
            ? new CsvBindingCollection<TValue>(list, isInternalCall: true)
            : null;
    }
}
