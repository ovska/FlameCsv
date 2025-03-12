using System.Diagnostics.Contracts;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Writing.Escaping;

internal interface ISimdEscaper<out T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    /// <summary>
    /// Returns a bitmask for characters in <paramref name="value"/> that need to be escape.
    /// </summary>
    /// <param name="value">Value to search in</param>
    /// <param name="needsQuoting">Vector reference of values that require the value to be quoted.</param>
    /// <returns>Bitmask containing characters that need to be escaped</returns>
    [Pure] uint FindEscapable(ref readonly TVector value, ref TVector needsQuoting);
}

// we are lazy and do only one check for Unix escaping
