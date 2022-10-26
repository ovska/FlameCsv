using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Providers;

public class MultiHeaderBindingProvider<T> : ICsvHeaderBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    public IList<ICsvHeaderBindingProvider<T>> Providers => _providers;

    private readonly List<ICsvHeaderBindingProvider<T>> _providers = new();
    private ICsvBindingProvider<T>? _winningProvider;

    public MultiHeaderBindingProvider(params ICsvHeaderBindingProvider<T>[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        Guard.IsNotEmpty(providers);

        for (var index = 0; index < providers.Length; index++)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (providers[index] is null)
                ThrowHelper.ThrowArgumentException($"Provider at index {index} was null");
        }

        _providers.AddRange(providers);
    }

    public bool TryGetBindings<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        if (_winningProvider is null)
            ThrowHelper.ThrowInvalidOperationException("No provider picked");

        return _winningProvider.TryGetBindings(out bindings);
    }

    public bool TryProcessHeader(ReadOnlySpan<T> line, CsvConfiguration<T> configuration)
    {
        foreach (var provider in _providers)
        {
            if (provider.TryProcessHeader(line, configuration))
            {
                _winningProvider = provider;
                return true;
            }
        }

        return false;
    }
}
