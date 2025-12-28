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
    /**/
    // Enum<PoisonPagePlacement>()
    [PoisonPagePlacement.None]
    /**/
    ;

    public static T[] Enum<T>()
        where T : struct, Enum => System.Enum.GetValues<T>();
}
