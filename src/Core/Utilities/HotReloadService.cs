using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.Utilities;

/// <summary>
/// Provides API to clear caches dependent on types on hot reload.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class HotReloadService
{
    public static bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MetadataUpdater.IsSupported;
    }

    /// <summary>
    /// Contains registered callbacks for cache clearing.
    /// </summary>
    private static readonly ConditionalWeakTable<object, Action<object>> _callbacks = MetadataUpdater.IsSupported
        ? []
        : null!;

    /// <summary>
    /// Clears the cache by invoking all registered hot reload callbacks.
    /// </summary>
    /// <remarks>Called by the framework</remarks>
    internal static void ClearCache(Type[]? types)
    {
        if (_callbacks is null)
            return; // should never happen

        foreach ((object state, Action<object> callback) in _callbacks)
        {
            callback(state);
        }
    }

    /// <summary>
    /// Registers an instance and its associated hot reload callback.
    /// </summary>
    /// <param name="instance">The instance to register.</param>
    /// <param name="onHotReload">The callback to invoke on the instance when a hot reload occurs.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterForHotReload(object instance, [RequireStaticDelegate] Action<object> onHotReload)
    {
        if (MetadataUpdater.IsSupported)
        {
            _callbacks.AddOrUpdate(instance, onHotReload);
        }
    }

    /// <summary>
    /// Unregisters an instance from hot reload notifications.
    /// </summary>
    /// <param name="instance">The instance to unregister.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterForHotReload(object instance)
    {
        if (MetadataUpdater.IsSupported)
        {
            _callbacks.Remove(instance);
        }
    }
}
