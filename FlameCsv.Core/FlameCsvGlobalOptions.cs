using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Contains global options for FlameCSV configurable through environment variables.
/// </summary>
[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FlameCsvGlobalOptions
{
    /// <summary>
    /// Maximum number of CSV fields to read ahead. Defaults to 4096, minimum is 32.
    /// Use <c>FLAMECSV_MAX_READAHEAD</c> to override.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The property is modified after using the library or using the getter.
    /// </exception>
    public static int ReadAheadCount
    {
        get => _readAheadCount ??= GetReadAheadFromEnvironment();
        set
        {
            if (_readAheadCount.HasValue && _readAheadCount != value) ThrowMutation();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 32);
            _readAheadCount = value;
        }
    }

    /// <summary>
    /// Indicates whether the application is short-lived (<c>FLAMECSV_DISABLE_CACHING</c> environment variable is set).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The property is modified after using the library or using the getter.
    /// </exception>
    public static bool CachingDisabled
    {
        get => _cachingDisabled ??= GetCachingFromEnvironment();
        set
        {
            if (_cachingDisabled.HasValue && _cachingDisabled != value) ThrowMutation();
            _cachingDisabled = value;
        }
    }

    private static int? _readAheadCount;
    private static bool? _cachingDisabled;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GetCachingFromEnvironment()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLAMECSV_DISABLE_CACHING"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetReadAheadFromEnvironment()
    {
        string? configured = Environment.GetEnvironmentVariable("FLAMECSV_MAX_READAHEAD");

        if (!string.IsNullOrEmpty(configured) && int.TryParse(configured, out var value))
        {
            return Math.Max(32, value);
        }

        return 4096;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMutation([CallerMemberName] string propertyName = "")
    {
        throw new NotSupportedException(
            $"{propertyName} can only be modified once at the very start of the application before FlameCSV is used.");
    }
}
