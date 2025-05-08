using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Reflection;

namespace FlameCsv.Utilities.Comparers;

internal sealed class Utf8Comparer
    : IEqualityComparer<StringLike>,
        IEqualityComparer<string>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, string>
{
    public static Utf8Comparer Ordinal { get; } = new(ignoreCase: false);
    public static Utf8Comparer OrdinalIgnoreCase { get; } = new(ignoreCase: true);

    private readonly bool _ignoreCase;

    private IAlternateEqualityComparer<ReadOnlySpan<char>, string?> Comparer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return (IAlternateEqualityComparer<ReadOnlySpan<char>, string?>)(
                _ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
            );
        }
    }

    private Utf8Comparer(bool ignoreCase) => _ignoreCase = ignoreCase;

    public bool Equals(StringLike x, StringLike y) => Comparer.Equals(x, y);

    public int GetHashCode(StringLike obj) => Comparer.GetHashCode(obj);

    public StringLike Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public bool Equals(ReadOnlySpan<byte> alternate, StringLike other)
    {
        return Utf8Util.WithBytes(
            alternate,
            (other: other.Value, @this: this),
            static (alternate, state) => state.@this.Comparer.Equals(alternate, state.other)
        );
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        return Utf8Util.WithBytes(alternate, this, static (alternate, @this) => @this.Comparer.GetHashCode(alternate));
    }

    bool IAlternateEqualityComparer<ReadOnlySpan<byte>, string>.Equals(ReadOnlySpan<byte> alternate, string other)
    {
        return Equals(alternate, (StringLike)other);
    }

    string IAlternateEqualityComparer<ReadOnlySpan<byte>, string>.Create(ReadOnlySpan<byte> alternate)
    {
        return Create(alternate);
    }

    bool IEqualityComparer<string>.Equals(string? x, string? y)
    {
        if (x is null)
        {
            return y is null;
        }

        if (y is null)
        {
            return false;
        }

        return Equals((StringLike)x, (StringLike)y);
    }

    int IEqualityComparer<string>.GetHashCode(string obj)
    {
        return GetHashCode((StringLike)obj);
    }
}
