using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Reflection;

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
        var typeInfo = CsvTypeInfo<TValue>.Instance;
        List<CsvBinding<TValue>> list = new();

        // Member attributes
        foreach (var member in typeInfo.Members)
        {
            foreach (var attribute in member.Attributes)
            {
                if (attribute is CsvIndexAttribute { Index: var index })
                {
                    list.Add(new MemberCsvBinding<TValue>(index, member));
                    break;
                }
            }
        }

        // Type targeted attributes
        foreach (var attr in typeInfo.Attributes)
        {
            if (attr is CsvIndexTargetAttribute target)
            {
                list.Add(new MemberCsvBinding<TValue>(target.Index, typeInfo.GetPropertyOrField(target.MemberName)));
            }
            else if (attr is CsvIndexIgnoreAttribute ignoreAttribute)
            {
                list.Add(new IgnoredCsvBinding<TValue>(ignoreAttribute.Index));
            }
        }

        // Primary constructor parameters
        foreach (var parameter in typeInfo.ConstructorParameters)
        {
            bool found = false;

            foreach (var attr in parameter.Attributes)
            {
                if (attr is CsvIndexAttribute { Index: var index })
                {
                    list.Add(new ParameterCsvBinding<TValue>(index, parameter));
                    found = true;
                    break;
                }
            }

            if (!found && !parameter.Value.HasDefaultValue)
            {
                throw new CsvBindingException<TValue>(parameter.Value);
            }
        }

        return list.Count > 0
            ? new CsvBindingCollection<TValue>(list, isInternalCall: true)
            : null;
    }
}
