using System.Diagnostics.Contracts;
using FlameCsv.Intrinsics;

namespace FlameCsv.Writing.Escaping;

internal interface ISimdEscaper<out T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    /// <summary>
    /// Character used for escaping.
    /// </summary>
    T Escape { get; }

    /// <summary>
    /// Returns a bitmask for characters in <paramref name="value"/> that need to be escape.
    /// </summary>
    /// <param name="value">Value to search in</param>
    /// <param name="needsQuoting">Vector reference of values that require the value to be quoted.</param>
    /// <returns>Bitmask containing characters that need to be escaped</returns>
    [Pure] uint FindEscapable(ref readonly TVector value, ref TVector needsQuoting);
}
