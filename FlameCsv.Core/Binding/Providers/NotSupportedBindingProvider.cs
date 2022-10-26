using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Providers;

internal sealed class NotSupportedBindingProvider<T> : ICsvHeaderBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    internal static readonly NotSupportedBindingProvider<T> Instance = new();
    
    [DoesNotReturn]
    public bool TryGetBindings<TValue>([NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        throw NotSupported;
    }

    [DoesNotReturn]
    public bool TryProcessHeader(ReadOnlySpan<T> line, CsvConfiguration<T> configuration)
    {
        throw NotSupported;
    }

    private NotSupportedException NotSupported
        => new($"Default header binding is not supported for token type {typeof(T).ToTypeString()}");
}
