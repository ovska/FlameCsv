namespace FlameCsv.Extensions;

internal static class EnumExtensions
{
    /// <summary>
    /// Returns true if <see cref="SecurityLevel.NoBufferClearing"/> is not set.
    /// </summary>
    public static bool ClearBuffers(this SecurityLevel security)
    {
        return (security & SecurityLevel.NoBufferClearing) != SecurityLevel.NoBufferClearing;
    }
}
