using System.Collections.Frozen;
using System.Runtime.InteropServices;
using FlameCsv.Attributes;
using FlameCsv.Utilities;

namespace FlameCsv.Reflection;

internal static class AssemblyAttributes
{
    private static Lazy<FrozenDictionary<Type, List<CsvConfigurationAttribute>>>? _attributes;

    public static ReadOnlySpan<CsvConfigurationAttribute> Get(Type type)
    {
        Lazy<FrozenDictionary<Type, List<CsvConfigurationAttribute>>>? local = _attributes;

        if (local is null)
        {
            var newInstance = new Lazy<FrozenDictionary<Type, List<CsvConfigurationAttribute>>>(InitializeAttributes);
            local = Interlocked.CompareExchange(ref _attributes, newInstance, null) ?? _attributes;

            // check if we actually replaced the field
            if (ReferenceEquals(local, newInstance))
            {
                // hot reload service needs a key for the weakref table, so pass the instance
                HotReloadService.RegisterForHotReload(
                    newInstance,
                    static _ => Interlocked.Exchange(ref _attributes, null)
                );
            }
        }

        if (local.Value.TryGetValue(type, out var list))
        {
            return CollectionsMarshal.AsSpan(list);
        }

        return ReadOnlySpan<CsvConfigurationAttribute>.Empty;
    }

    private static FrozenDictionary<Type, List<CsvConfigurationAttribute>> InitializeAttributes()
    {
        var attributes = new Dictionary<Type, List<CsvConfigurationAttribute>>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (
                CsvConfigurationAttribute attribute in assembly.GetCustomAttributes(
                    typeof(CsvConfigurationAttribute),
                    inherit: false
                )
            )
            {
                ref List<CsvConfigurationAttribute>? list = ref CollectionsMarshal.GetValueRefOrAddDefault(
                    attributes,
                    attribute.TargetType,
                    out _
                );

                (list ??= []).Add(attribute);
            }
        }

        return attributes.ToFrozenDictionary();
    }
}
