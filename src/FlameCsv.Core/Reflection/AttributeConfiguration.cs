using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;

namespace FlameCsv.Reflection;

internal static class AttributeConfiguration
{
    internal static readonly TrimmingCache<Type, HeaderDataEntry> Cache
        = FlameCsvGlobalOptions.CachingDisabled ? null! : [];

    internal sealed class HeaderData(
        List<BindingData> candidates,
        ImmutableArray<int> ignoredIndexes)
    {
        public ReadOnlySpan<BindingData> Value => candidates.AsSpan();
        public ReadOnlySpan<int> IgnoredIndexes => ignoredIndexes.AsSpan();
    }

    internal sealed class HeaderDataEntry
    {
        public HeaderDataEntry(Func<HeaderData> read, Func<HeaderData> write)
        {
            _read = new(read);
            _write = new(write);
        }

        public HeaderData Read => _read.Value;
        public HeaderData Write => _write.Value;

        private readonly Lazy<HeaderData> _read;
        private readonly Lazy<HeaderData> _write;
    }

    /// <summary>
    /// Returns members of <typeparamref name="TValue"/> that can be used for binding.
    /// </summary>
    [RDC(Messages.Reflection)]
    public static HeaderData GetFor<[DAM(Messages.ReflectionBound)] TValue>(bool write)
    {
        if (FlameCsvGlobalOptions.CachingDisabled || !Cache.TryGetValue(typeof(TValue), out var entry))
        {
            entry = new HeaderDataEntry(
                read: static () => Create(CsvTypeInfo<TValue>.Value.ProxyOrSelf, write: false),
                write: static () => Create(CsvTypeInfo<TValue>.Value, write: true));

            if (!FlameCsvGlobalOptions.CachingDisabled)
            {
                Cache.Add(typeof(TValue), entry);
            }
        }

        return write ? entry.Write : entry.Read;
    }

    private static HeaderData Create(CsvTypeInfo typeInfo, bool write)
    {
        List<BindingData> candidates = [];
        HashSet<int>? ignoredIndexes = null;

        Dictionary<(string name, bool isParameter), List<CsvFieldConfigurationAttribute>> dict = [];

        void AddAttribute(
            CsvFieldConfigurationAttribute baseAttr,
            string? knownName = null,
            bool? knownParameter = null)
        {
            string name = knownName ?? baseAttr.MemberName;
            bool isParameter = knownParameter ?? baseAttr.IsParameter;

            if (write && isParameter) return;

            if (string.IsNullOrEmpty(name))
            {
                throw new CsvConfigurationException(
                    $"The {baseAttr.GetType().Name} attribute must specify a member name (on type {typeInfo.Type.FullName}).");
            }

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, (name, isParameter), out _);
            (list ??= []).Add(baseAttr);
        }

        foreach (var attr in AssemblyAttributes.Get(typeInfo.Type))
        {
            if (attr is CsvFieldConfigurationAttribute baseAttr)
            {
                AddAttribute(baseAttr);
            }
            else if (attr is CsvIgnoredIndexesAttribute ignored)
            {
                foreach (var index in ignored.Value)
                {
                    (ignoredIndexes ??= []).Add(index);
                }
            }
        }

        foreach (var attr in typeInfo.Attributes)
        {
            if (attr is CsvFieldConfigurationAttribute baseAttr)
            {
                AddAttribute(baseAttr);
            }
            else if (attr is CsvIgnoredIndexesAttribute ignored)
            {
                foreach (var index in ignored.Value)
                {
                    (ignoredIndexes ??= []).Add(index);
                }
            }
        }

        foreach (var member in typeInfo.Members)
        {
            if (!write && member.IsReadOnly) continue;

            bool found = false;

            foreach (var attr in member.Attributes)
            {
                if (attr is CsvFieldConfigurationAttribute baseAttr)
                {
                    AddAttribute(baseAttr, knownName: member.Value.Name, knownParameter: false);
                    found = true;
                }
            }

            if (!found)
            {
                // add default
                candidates.Add(
                    new BindingData
                    {
                        Name = member.Value.Name, Target = member.Value, Aliases = [],
                    });
            }
        }

        foreach (var parameter in !write ? typeInfo.ConstructorParameters : default)
        {
            bool found = false;

            foreach (var attr in parameter.Attributes)
            {
                if (attr is CsvFieldConfigurationAttribute baseAttr)
                {
                    AddAttribute(baseAttr, knownName: parameter.Value.Name, knownParameter: true);
                    found = true;
                }
            }

            if (!found)
            {
                // add default
                candidates.Add(
                    new BindingData
                    {
                        Name = parameter.Value.Name!, Target = parameter.Value, Aliases = [],
                    });
            }
        }

        foreach (((string name, bool isParameter), List<CsvFieldConfigurationAttribute> list) in dict)
        {
            string? value = null;
            ImmutableArray<string> aliases = [];
            int? order = null;
            int? index = null;
            bool ignored = false;
            bool required = false;

            foreach (var attr in list)
            {
                if (attr is CsvRequiredAttribute)
                {
                    required = true;
                }
                else if (attr is CsvIgnoreAttribute)
                {
                    ignored = true;
                }
                else if (attr is CsvHeaderAttribute headerAttr)
                {
                    if (value is null)
                    {
                        value = headerAttr.Value;
                        aliases = headerAttr.Aliases;
                    }
                    else
                    {
                        throw new CsvBindingException(
                            typeInfo.Type,
                            $"Multiple {nameof(CsvHeaderAttribute)} attributes found for {name}.");
                    }
                }
                else if (attr is CsvOrderAttribute orderAttr)
                {
                    if (order is null)
                    {
                        order = orderAttr.Order;
                    }
                    else
                    {
                        throw new CsvBindingException(
                            typeInfo.Type,
                            $"Multiple order attributes found for {name}.");
                    }
                }
                else if (attr is CsvIndexAttribute indexAttr)
                {
                    if (index is null)
                    {
                        index = indexAttr.Index;
                    }
                    else
                    {
                        throw new CsvBindingException(
                            typeInfo.Type,
                            $"Multiple index attributes found for {name}.");
                    }
                }
            }

            if (ignored && required)
            {
                throw new CsvBindingException(
                    $"A CSV field cannot be both required and ignored ({(isParameter ? "param" : "member")} {name}).");
            }

            candidates.Add(
                new BindingData
                {
                    Name = value ?? name,
                    Aliases = aliases,
                    Order = order ?? 0,
                    Target = isParameter ? typeInfo.GetParameter(name).Value : typeInfo.GetPropertyOrField(name).Value,
                    Ignored = ignored,
                    Required = required,
                    Index = index,
                });
        }

        foreach (var parameter in !write ? typeInfo.ConstructorParameters : default)
        {
            // hack for records: remove init-only properties with the same name and type
            // as a parameter
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                BindingData existing = candidates[i];

                if (existing.Target is PropertyInfo prop &&
                    prop.Name == parameter.Value.Name &&
                    prop.PropertyType == parameter.Value.ParameterType &&
                    prop.SetMethod is { ReturnParameter: var rp } &&
                    rp.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        candidates.Sort();

        int[] ignoreList = ignoredIndexes?.ToArray() ?? [];
        ignoreList.AsSpan().Sort();

        return new HeaderData(candidates, ImmutableCollectionsMarshal.AsImmutableArray(ignoreList));
    }
}
