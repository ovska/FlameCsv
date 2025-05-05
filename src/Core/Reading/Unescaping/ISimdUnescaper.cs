// ReSharper disable once UnusedMemberInSuper.Global

namespace FlameCsv.Reading.Unescaping;

internal interface ISimdUnescaper
{
    /// <summary>
    /// Returns <c>true</c> if the unescaper is supported on the current hardware.
    /// </summary>
    static abstract bool IsSupported { get; }

    /// <summary>
    /// Returns the number of characters handled per iteration.
    /// </summary>
    static abstract int Count { get; }
}

internal interface ISimdUnescaper<T, TMask, TVector> : ISimdUnescaper
    where T : unmanaged, IBinaryInteger<T>
    where TMask : unmanaged, IBinaryInteger<TMask>, IUnsignedNumber<TMask>
    where TVector : struct
{
    /// <summary>
    /// Creates a vector with all values set to the specified value.
    /// </summary>
    static abstract TVector CreateVector(T value);

    /// <summary>
    /// Loads a vector from the specified memory location.
    /// </summary>
    static abstract TVector LoadVector(ref readonly T value, nuint offset = 0);

    /// <summary>
    /// Writes the vector to the specified memory location.
    /// </summary>
    static abstract void StoreVector(TVector vector, ref T destination, nuint offset = 0);

    /// <summary>
    /// Returns a bitmask for quote positions in the specified value.
    /// </summary>
    static abstract TMask FindQuotes(TVector value, TVector quote);

    /// <summary>
    /// Writes the value unescaped to destination.
    /// </summary>
    static abstract void Compress(TVector value, TMask mask, ref T destination, nuint offset = 0);
}
