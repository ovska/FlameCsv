using System.Collections.Frozen;
using System.Runtime.InteropServices;
using FlameCsv.Attributes;
using FlameCsv.Utilities;

namespace FlameCsv.Reflection;

internal static class AssemblyAttributes
{
    private static Lazy<FrozenDictionary<Type, List<object>>>? _attributes;

    public static ReadOnlySpan<object> Get(Type type)
    {
        Lazy<FrozenDictionary<Type, List<object>>>? local = _attributes;

        if (local is null)
        {
            var newInstance = new Lazy<FrozenDictionary<Type, List<object>>>(InitializeAttributes);
            local = Interlocked.CompareExchange(ref _attributes, newInstance, null) ?? _attributes;

            // check if we actually replaced the field
            if (ReferenceEquals(local, newInstance))
            {
                // hot reload service needs a key for the weakref table, so pass the instance
                HotReloadService.RegisterForHotReload(
                    newInstance,
                    static _ => Interlocked.Exchange(ref _attributes, null));
            }
        }

        if (local.Value.TryGetValue(type, out var list))
        {
            return CollectionsMarshal.AsSpan(list);
        }

        return ReadOnlySpan<object>.Empty;
    }

    private static FrozenDictionary<Type, List<object>> InitializeAttributes()
    {
        var attributes = new Dictionary<Type, List<object>>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var attribute in assembly.GetCustomAttributes(
                         typeof(CsvTypeConfigurableBaseAttribute),
                         inherit: false))
            {
                ref List<object>? list = ref CollectionsMarshal.GetValueRefOrAddDefault(
                    attributes,
                    ((CsvTypeConfigurableBaseAttribute)attribute).TargetType,
                    out _);

                (list ??= []).Add(attribute);
            }
        }

        return attributes.ToFrozenDictionary();
    }
}
