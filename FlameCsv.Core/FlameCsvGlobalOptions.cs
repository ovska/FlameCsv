using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Contains global options for FlameCsv configurable through environment variables.
/// </summary>
[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FlameCsvGlobalOptions
{
    /// <summary>
    /// Default size for the field read ahead buffer.
    /// </summary>
    /// <seealso cref="ReadAheadCount"/>
    public const int DefaultReadAheadCount = 4096;

    /// <summary>
    /// Default number of CSV fields to read ahead. Defaults to <see cref="DefaultReadAheadCount"/>, minimum is 32.
    /// <br/>Environment variable <c>FLAMECSV_MAX_READAHEAD</c> can be used to override.
    /// </summary>
    /// <remarks>
    /// This is only the initial value.
    /// If a record has more fields than the read ahead buffer, the buffer will be resized to accommodate
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The property is modified after using the library or using the getter.
    /// </exception>
    public static int ReadAheadCount
    {
        get => _readAheadCount ??= GetReadAheadFromEnvironment();
        set
        {
            if (value == _readAheadCount)
                return;
            if (_readAheadCount.HasValue && _readAheadCount != value)
                ThrowMutation();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 32);
            _readAheadCount = value;
        }
    }

    /// <summary>
    /// Indicates whether the application is short-lived.
    /// <br/>Environment variable <c>FLAMECSV_DISABLE_CACHING</c> can be set to disable caching.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The property is modified after using the library or using the getter.
    /// </exception>
    public static bool CachingDisabled
    {
        get => _cachingDisabled ??= GetCachingFromEnvironment();
        set
        {
            if (value == _cachingDisabled)
                return;
            if (_cachingDisabled.HasValue && _cachingDisabled != value)
                ThrowMutation();
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

        return DefaultReadAheadCount;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMutation([CallerMemberName] string propertyName = "")
    {
        throw new NotSupportedException(
            $"{propertyName} can only be modified once at the very start of the application before FlameCsv is used."
        );
    }
}
