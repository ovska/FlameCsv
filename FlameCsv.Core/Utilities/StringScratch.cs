using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Utilities;

[InlineArray(MaxLength)]
internal struct StringScratch
{
    public string elem0;
    public const int MaxLength = 16;
}

internal static class StringScratchExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<string> AsSpan(ref this StringScratch scratch, int length)
    {
        return ((Span<string>)scratch).Slice(0, length);
    }
}

internal readonly struct StackHeaders : IEquatable<StackHeaders>
{
    public int Length { get; }
    private readonly IEqualityComparer<string> _comparer;

    public StackHeaders(IEqualityComparer<string> comparer, scoped ReadOnlySpan<string> value)
    {
        _comparer = comparer;

        Length = value.Length;
        value.CopyTo(MemoryMarshal.CreateSpan(ref this._elem0, 16)!);
    }

    public ReadOnlySpan<string> AsSpan()
    {
        return MemoryMarshal.CreateReadOnlySpan(in this._elem0, Length)!;
    }

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
#pragma warning disable CS0169 // Field is never used
    private readonly string? _elem0;
    private readonly string? _elem1;
    private readonly string? _elem2;
    private readonly string? _elem3;
    private readonly string? _elem4;
    private readonly string? _elem5;
    private readonly string? _elem6;
    private readonly string? _elem7;
    private readonly string? _elem8;
    private readonly string? _elem9;
    private readonly string? _elem10;
    private readonly string? _elem11;
    private readonly string? _elem12;
    private readonly string? _elem13;
    private readonly string? _elem14;
    private readonly string? _elem15;
#pragma warning restore CS0169 // Field is never used

    public override bool Equals(object? obj)
    {
        return obj is StackHeaders other && Equals(other);
    }

    public bool Equals(StackHeaders other)
    {
        if (Length != other.Length || !ReferenceEquals(_comparer, other._comparer))
        {
            return false;
        }

        ReadOnlySpan<string> span = AsSpan();
        ReadOnlySpan<string> otherSpan = other.AsSpan();

        for (int i = 0; i < Length; i++)
        {
            if (!_comparer.Equals(span[i], otherSpan[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(Length);

        foreach (var value in AsSpan())
        {
            hash.Add(_comparer.GetHashCode(value));
        }

        return hash.ToHashCode();
    }
}
