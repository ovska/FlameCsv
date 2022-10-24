using System.Diagnostics.CodeAnalysis;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding.Providers;

public class MultiBindingProvider<T> : ICsvBindingProvider<T> where T : unmanaged, IEquatable<T>
{
    public IList<ICsvBindingProvider<T>> Providers => _providers;

    private readonly List<ICsvBindingProvider<T>> _providers = new();

    public MultiBindingProvider(params ICsvBindingProvider<T>[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers.AddRange(providers);
    }

    public MultiBindingProvider()
    {
    }

    public virtual MultiBindingProvider<T> Add(ICsvBindingProvider<T> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
        return this;
    }

    public virtual bool TryGetBindings<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        if (_providers.Count == 0)
            throw new CsvBindingException("No providers added to MultiBindingProvider");

        foreach (var provider in _providers)
        {
            if (provider.TryGetBindings(out bindings))
                return true;
        }

        bindings = default;
        return false;
    }
}
