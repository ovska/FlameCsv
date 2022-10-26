using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding.Providers;

/// <summary>
/// Provider that attempts to bind using multiple configured providers, and picks the first successful binding.
/// The providers are processed LIFO i.e. the last provider added is the first to be checked.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public class MultiBindingProvider<T> : ICsvBindingProvider<T> where T : unmanaged, IEquatable<T>
{
    public IList<ICsvBindingProvider<T>> Providers => _providers;

    private readonly List<ICsvBindingProvider<T>> _providers = new();

    public MultiBindingProvider(params ICsvBindingProvider<T>[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        Guard.IsNotEmpty(providers);

        for (var index = 0; index < providers.Length; index++)
        {
            switch (providers[index])
            {
                case ICsvHeaderBindingProvider<T>:
                    ThrowHelper.ThrowNotSupportedException(
                        $"Provider at index {index} was a header binding provider, "
                        + $"use {typeof(MultiHeaderBindingProvider<T>)} instead.");
                    break;
                case null:
                    ThrowHelper.ThrowArgumentException($"Provider at index {index} was null");
                    break;
            }
        }

        _providers.AddRange(providers);
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

        for (int i = _providers.Count - 1; i >= 0; i--)
        {
            if (_providers[i].TryGetBindings(out bindings))
                return true;
        }

        bindings = default;
        return false;
    }
}
