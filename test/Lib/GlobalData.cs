namespace FlameCsv.Tests;

public static class GlobalData
{
    /// <summary>
    /// True and false.
    /// </summary>
    public static bool[] Booleans { get; } = [true, false];

    /// <summary>
    /// Guarded memory allocation data.
    /// <see langword="null"/> means non-guarded date,
    /// <c>true</c> means data is guarded right after the memory region,
    /// <c>false</c> means the guarded data is right before.
    /// </summary>
    public static PoisonPagePlacement[] PoisonPlacement { get; } =
    [
        PoisonPagePlacement.None,
#if FULL_TEST_SUITE
        // PoisonPagePlacement.After,
        // PoisonPagePlacement.Before,
#endif
    ];

    public static T[] Enum<T>()
        where T : struct, Enum => System.Enum.GetValues<T>();
}
