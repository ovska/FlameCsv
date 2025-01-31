using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

internal static class IndexAttributeBinder<[DAM(Messages.ReflectionBound)] TValue>
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
        List<CsvBinding<TValue>> list = [];
        List<CsvTypeFieldAttribute> parameterAttributes = [];

        foreach (var attr in CsvTypeInfo.Attributes<TValue>())
        {
            if (attr is CsvTypeAttribute typeAttr)
            {
                foreach (var ignoredIndex in typeAttr.IgnoredIndexes ?? [])
                {
                    list.Add(new IgnoredCsvBinding<TValue>(ignoredIndex));
                }

                continue;
            }

            if (attr is not CsvTypeFieldAttribute { Index: var index and >= 0 } fieldAttribute)
            {
                continue;
            }

            if (fieldAttribute.IsIgnored)
            {
                list.Add(new IgnoredCsvBinding<TValue>(index));
                continue;
            }

            if (fieldAttribute.IsParameter)
            {
                if (!write)
                {
                    parameterAttributes.Add(fieldAttribute);
                }

                continue;
            }

            list.Add(
                new MemberCsvBinding<TValue>(
                    index,
                    CsvTypeInfo.GetPropertyOrField<TValue>(fieldAttribute.MemberName)));
        }

        foreach (var member in CsvTypeInfo.Members<TValue>())
        {
            foreach (var attr in member.Attributes)
            {
                if (attr is CsvFieldAttribute { Index: var index and >= 0 } fieldAttribute)
                {
                    if (fieldAttribute.IsIgnored)
                    {
                        list.Add(new IgnoredCsvBinding<TValue>(index));
                        break;
                    }

                    list.Add(new MemberCsvBinding<TValue>(index, member));
                    break;
                }
            }
        }

        if (!write)
        {
            foreach (var targetAttr in parameterAttributes)
            {
                Debug.Assert(targetAttr.Index >= 0);

                ParameterData? match = null;

                foreach (var parameter in CsvTypeInfo.ConstructorParameters<TValue>())
                {
                    if (parameter.Value.Name == targetAttr.MemberName)
                    {
                        match = parameter;
                        break;
                    }
                }

                if (match is null)
                {
                    throw new CsvBindingException<TValue>($"Parameter '{targetAttr.MemberName}' not found");
                }

                list.Add(new ParameterCsvBinding<TValue>(targetAttr.Index, match.Value));
            }

            // TODO: find match from parameters

            foreach (var parameter in CsvTypeInfo.ConstructorParameters<TValue>())
            {
                bool found = false;

                foreach (var attr in parameter.Attributes)
                {
                    if (attr is CsvFieldAttribute { Index: { } index })
                    {
                        list.Add(new ParameterCsvBinding<TValue>(index, parameter));
                        found = true;
                        break;
                    }
                }

                if (!found && !parameter.HasDefaultValue)
                {
                    throw new CsvBindingException<TValue>(parameter.Value, Array.Empty<CsvBinding>());
                }
            }
        }

        return list.Count > 0
            ? new CsvBindingCollection<TValue>(FixGaps(list, write), write)
            : null;
    }

    private static IEnumerable<CsvBinding<TValue>> FixGaps(List<CsvBinding<TValue>> allBindings, bool write)
    {
        SortedDictionary<int, List<CsvBinding<TValue>>> dict = [];

        foreach (var binding in allBindings)
        {
            if (!dict.TryGetValue(binding.Index, out var list))
            {
                dict.Add(binding.Index, list = []);
            }

            // don't add duplicates
            if (list.Contains(binding, CsvBinding<TValue>.TargetComparer))
            {
                continue;
            }

            list.Add(binding);
        }

        foreach ((_, List<CsvBinding<TValue>> bindings) in dict)
        {
            CsvBinding<TValue> first = bindings[0];

            if (bindings.Count == 1)
            {
                yield return first;
                continue;
            }

            // mix of ignored and non-ignored
            if (bindings.Exists(static b => b.IsIgnored))
            {
                throw new CsvBindingException<TValue>(
                    $"Index {first.Index} has a mix of ignored and non-ignored bindings",
                    bindings);
            }

            if (!write)
            {
                CsvBinding<TValue>? parameter = null;

                foreach (var binding in bindings)
                {
                    if (binding is ParameterCsvBinding<TValue>)
                    {
                        if (parameter is not null)
                        {
                            throw new CsvBindingException<TValue>(
                                $"Index {first.Index} has multiple parameter bindings",
                                bindings);
                        }

                        parameter = binding;
                    }

                    // must be a member binding
                    Debug.Assert(binding is MemberCsvBinding<TValue>);
                }

                if (parameter is null)
                {
                    throw new CsvBindingException<TValue>(
                        $"Index {first.Index} has multiple member bindings",
                        bindings);
                }

                yield return parameter;
                continue;
            }

            throw new CsvBindingException<TValue>(
                $"Could not determine the binding to use for index {first.Index} ",
                bindings);
        }
    }
}
