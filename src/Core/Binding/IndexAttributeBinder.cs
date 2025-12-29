using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

[RDC(Messages.Reflection)]
internal static class IndexAttributeBinder<[DAM(Messages.ReflectionBound)] TValue>
{
    private static readonly Lazy<CsvBindingCollection<TValue>?> _read = new(() => CreateBindingCollection(false));
    private static readonly Lazy<CsvBindingCollection<TValue>?> _write = new(() => CreateBindingCollection(true));

    public static bool TryGetBindings(bool write, [NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        return (bindings = (write ? _write : _read).Value) is not null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CsvBindingCollection<TValue>? CreateBindingCollection(bool write)
    {
        List<CsvBinding<TValue>> list = [];

        var configuration = AttributeConfiguration.GetFor<TValue>(write);

        foreach (var data in configuration.Value)
        {
            if (data.Index is not { } index)
            {
                continue;
            }

            if (data.Ignored)
            {
                list.Add(new IgnoredCsvBinding<TValue>(index));
                continue;
            }

            list.Add(CsvBinding.FromBindingData<TValue>(index, in data));
        }

        foreach (var index in configuration.IgnoredIndexes)
        {
            list.Add(new IgnoredCsvBinding<TValue>(index));
        }

        // duplicate handling is only for headers
        return list.Count > 0
            ? new CsvBindingCollection<TValue>(FixGaps(list, write), write, ignoreDuplicates: false)
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
                throw new CsvBindingException(
                    $"Index {first.Index} has a mix of ignored and non-ignored bindings",
                    bindings
                )
                {
                    TargetType = typeof(TValue),
                };
            }

            // what is this doing?
            if (!write)
            {
                CsvBinding<TValue>? parameter = null;

                foreach (var binding in bindings)
                {
                    if (binding is ParameterCsvBinding<TValue>)
                    {
                        if (parameter is not null)
                        {
                            throw new CsvBindingException(
                                $"Index {first.Index} has multiple parameter bindings",
                                bindings
                            )
                            {
                                TargetType = typeof(TValue),
                            };
                        }

                        parameter = binding;
                        continue;
                    }

                    // must be a member binding
                    Check.True(binding is MemberCsvBinding<TValue>, $"Was: {binding.GetType().FullName}");
                }

                if (parameter is null)
                {
                    throw new CsvBindingException($"Index {first.Index} has multiple member bindings", bindings)
                    {
                        TargetType = typeof(TValue),
                    };
                }

                yield return parameter;
                continue;
            }

            throw new CsvBindingException($"Could not determine the binding to use for index {first.Index} ", bindings)
            {
                TargetType = typeof(TValue),
            };
        }
    }
}
