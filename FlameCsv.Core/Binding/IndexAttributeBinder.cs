using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

internal static class IndexAttributeBinder<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>
{
    private static readonly Lazy<CsvBindingCollection<TValue>?> _read
        = new(() => CreateBindingCollection(false));

    private static readonly Lazy<CsvBindingCollection<TValue>?> _write
        = new(() => CreateBindingCollection(true));

    public static bool TryGetBindings(
        bool write,
        [NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        return (bindings = (write ? _write : _read).Value) is not null;
    }

    private static CsvBindingCollection<TValue>? CreateBindingCollection(bool write)
    {
        var typeInfo = CsvTypeInfo<TValue>.Instance;
        List<CsvBinding<TValue>> list = [];

        foreach (var attr in typeInfo.Attributes)
        {
            if (attr is CsvIndexTargetAttribute target)
            {
                if (!target.Scope.IsValidFor(write))
                    continue;

                list.Add(new MemberCsvBinding<TValue>(target.Index, typeInfo.GetPropertyOrField(target.MemberName)));
            }
            else if (attr is CsvIndexIgnoreAttribute { Indexes: var ignoredIndices } ignoreAttr)
            {
                if (!ignoreAttr.Scope.IsValidFor(write))
                    continue;

                foreach (var index in ignoredIndices)
                {
                    // Ensure no duplicate ignores since its not really harmful to have them
                    if (!HasIgnoredIndex(index, list))
                        list.Add(new IgnoredCsvBinding<TValue>(index));
                }
            }
        }

        foreach (var member in typeInfo.Members)
        {
            foreach (var attr in member.Attributes)
            {
                if (attr is CsvIndexAttribute { Index: var index } indexAttr &&
                    indexAttr.Scope.IsValidFor(write))
                {
                    list.Add(new MemberCsvBinding<TValue>(index, member));
                    break;
                }
            }
        }

        if (!write)
        {
            foreach (var parameter in typeInfo.ConstructorParameters)
            {
                bool found = false;

                foreach (var attr in parameter.Attributes)
                {
                    if (attr is CsvIndexAttribute { Index: var index } indexAttr)
                    {
                        list.Add(new ParameterCsvBinding<TValue>(index, parameter));
                        found = true;
                        break;
                    }
                }

                if (!found && !parameter.Value.HasDefaultValue)
                {
                    throw new CsvBindingException<TValue>(parameter.Value, Array.Empty<CsvBinding>());
                }
            }
        }

        return list.Count > 0
            ? new CsvBindingCollection<TValue>(list, write, isInternalCall: true)
            : null;

        static bool HasIgnoredIndex(int index, List<CsvBinding<TValue>> list)
        {
            foreach (var binding in list.AsSpan())
            {
                if (binding.Index == index && binding.IsIgnored)
                    return true;
            }

            return false;
        }
    }
}
