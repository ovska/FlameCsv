using System.Collections.Immutable;

namespace FlameCsv.Tests;

internal static class GlobalData
{
    public static ImmutableArray<bool> Booleans { get; } = [true, false];

    public static ImmutableArray<bool?> GuardedMemory { get; } = OperatingSystem.IsWindows()
        ? [true, false, null]
        : [null];
}
