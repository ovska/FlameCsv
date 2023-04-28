using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Utilities;

internal struct ValueArray<T>
{
    private const int PreallocSize = 16;

    private T[]? _overflow;

    public readonly int Count => index;

    public void Push(T value)
    {
        switch (index)
        {
            case 0:
                field0 = value;
                break;
            case 1:
                field1 = value;
                break;
            case 2:
                field2 = value;
                break;
            case 3:
                field3 = value;
                break;
            case 4:
                field4 = value;
                break;
            case 5:
                field5 = value;
                break;
            case 6:
                field6 = value;
                break;
            case 7:
                field7 = value;
                break;
            case 8:
                field8 = value;
                break;
            case 9:
                field9 = value;
                break;
            case 10:
                field10 = value;
                break;
            case 11:
                field11 = value;
                break;
            case 12:
                field12 = value;
                break;
            case 13:
                field13 = value;
                break;
            case 14:
                field14 = value;
                break;
            case 15:
                field15 = value;
                break;
            default:
            {
                _overflow ??= new T[PreallocSize];

                int arrayIndex = index - PreallocSize;

                if (arrayIndex >= _overflow.Length)
                {
                    Array.Resize(ref _overflow, _overflow.Length * 2);
                }

                _overflow[arrayIndex] = value;
            }
            break;
        }

        index++;
    }

    public void Reset()
    {
        if (_overflow is null)
        {
            this = default;
        }
        else
        {
            ResetSlow();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PushSlow(T value)
    {
        _overflow ??= new T[PreallocSize];

        int arrayIndex = index - PreallocSize;

        if (arrayIndex >= _overflow.Length)
        {
            Array.Resize(ref _overflow, _overflow.Length * 2);
        }

        _overflow[arrayIndex] = value;
        index++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResetSlow()
    {
        Debug.Assert(_overflow is not null);

        _overflow.AsSpan(0, index - PreallocSize).Clear();

        index = default;
        field0 = default!;
        field1 = default!;
        field2 = default!;
        field3 = default!;
        field4 = default!;
        field5 = default!;
        field6 = default!;
        field7 = default!;
        field8 = default!;
        field9 = default!;
        field10 = default!;
        field11 = default!;
        field12 = default!;
        field13 = default!;
        field14 = default!;
        field15 = default!;
    }

    public readonly T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                ThrowHelper.ThrowArgumentOutOfRangeException<T>(nameof(index));

            return (uint)index switch
            {
                0 => field0,
                1 => field1,
                2 => field2,
                3 => field3,
                4 => field4,
                5 => field5,
                6 => field6,
                7 => field7,
                8 => field8,
                9 => field9,
                10 => field10,
                11 => field11,
                12 => field12,
                13 => field13,
                14 => field14,
                15 => field15,
                _ => _overflow![index - PreallocSize],
            };
        }
    }

    private int index;
    private T field0;
    private T field1;
    private T field2;
    private T field3;
    private T field4;
    private T field5;
    private T field6;
    private T field7;
    private T field8;
    private T field9;
    private T field10;
    private T field11;
    private T field12;
    private T field13;
    private T field14;
    private T field15;
}
