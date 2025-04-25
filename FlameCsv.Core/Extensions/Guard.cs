using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Extensions;

internal static class Guard
{
    /// <summary>
    /// Ensures the stream is readable and not null.
    /// </summary>
    public static void CanRead([NotNull] Stream? stream, [CallerArgumentExpression(nameof(stream))] string paramName = "")
    {
        ArgumentNullException.ThrowIfNull(stream, paramName);

        if (!stream.CanRead)
            Throw.Argument(paramName, "Stream is not readable");
    }

    /// <summary>
    /// Ensures the stream is writable and not null.
    /// </summary>
    public static void CanWrite([NotNull] Stream? stream, [CallerArgumentExpression(nameof(stream))] string paramName = "")
    {
        ArgumentNullException.ThrowIfNull(stream, paramName);

        if (!stream.CanWrite)
            Throw.Argument(paramName, "Stream is not writable");
    }
}
