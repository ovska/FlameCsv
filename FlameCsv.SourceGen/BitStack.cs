using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.SourceGen;

// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/src/System/Text/Json/BitStack.cs
internal struct BitStack
{
    // We are using a ulong to represent our nested state, so we can only
    // go 64 levels deep without having to allocate.
    private const byte MaxDepth = checked(sizeof(ulong) * 8);
    private byte _currentDepth;

    private ulong _allocationFreeContainer;

    private static void ThrowForTooManyFields()
    {
        throw new InvalidOperationException("Over 64 fields are not supported.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushTrue()
    {
        if (_currentDepth < MaxDepth)
        {
            _allocationFreeContainer = (_allocationFreeContainer << 1) | 1;
        }
        else
        {
            ThrowForTooManyFields();
        }
        _currentDepth++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushFalse()
    {
        if (_currentDepth < MaxDepth)
        {
            _allocationFreeContainer <<= 1;
        }
        else
        {
            ThrowForTooManyFields();
        }
        _currentDepth++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Pop()
    {
        _currentDepth--;

        if (_currentDepth < MaxDepth)
        {
            _allocationFreeContainer >>= 1;
            return (_allocationFreeContainer & 1) != 0;
        }
        else if (_currentDepth == MaxDepth)
        {
            return (_allocationFreeContainer & 1) != 0;
        }
        else
        {
            ThrowForTooManyFields();
            return default; // unreachable
        }
    }

    public void SetFirstBit()
    {
        Debug.Assert(_currentDepth == 0, "Only call SetFirstBit when depth is 0");
        _currentDepth++;
        _allocationFreeContainer = 1;
    }

    public void ResetFirstBit()
    {
        Debug.Assert(_currentDepth == 0, "Only call ResetFirstBit when depth is 0");
        _currentDepth++;
        _allocationFreeContainer = 0;
    }

    public static implicit operator ulong(BitStack stack) => stack._allocationFreeContainer;
    public static implicit operator BitStack(ulong value) => new() { _allocationFreeContainer = value };
}
