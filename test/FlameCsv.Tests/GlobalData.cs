﻿namespace FlameCsv.Tests;

internal static class GlobalData
{
    /// <summary>
    /// True and false.
    /// </summary>
    public static bool[] Booleans { get; } = [true, false];

    /// <summary>
    /// Guarded memory allocation data.
    /// <see langword="null"/> means non-guarded date,
    /// <see langword="true"/> means data is guarded right after the memory region,
    /// <see langword="false"/> means the guarded data is right before.
    /// </summary>
    public static bool?[] GuardedMemory { get; } =
        OperatingSystem.IsWindows() &&
        Environment.GetEnvironmentVariable("COMPlus_legacyCorruptedStateExceptionsPolicy") == "1"
            ? [true, false, null]
            : [null];


    public static T[] Enum<T>() where T : struct, Enum => EnumValues<T>.Values;

    private static class EnumValues<T> where T : struct, Enum
    {
        public static T[] Values { get; } = System.Enum.GetValues<T>();
    }
}
