using System.Buffers;

namespace FlameCsv.Fuzzing.Utilities;

internal sealed class BoundedBufferPool(PoisonPagePlacement placement = PoisonPagePlacement.After) : IBufferPool
{
    public IMemoryOwner<byte> GetBytes(int length)
    {
        if (length == -1)
            length = 256;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        return PooledBoundedMemory<byte>.Rent((int)BitOperations.RoundUpToPowerOf2((uint)length), placement);
    }

    public IMemoryOwner<char> GetChars(int length)
    {
        if (length == -1)
            length = 256;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        return PooledBoundedMemory<char>.Rent((int)BitOperations.RoundUpToPowerOf2((uint)length), placement);
    }
}
