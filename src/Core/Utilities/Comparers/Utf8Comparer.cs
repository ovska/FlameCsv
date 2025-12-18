using System.Text;
using FlameCsv.Reflection;

namespace FlameCsv.Utilities.Comparers;

internal sealed class Utf8Comparer(StringComparer comparer)
    : IEqualityComparer<StringLike>,
        IEqualityComparer<string>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, string>
{
    public static Utf8Comparer Ordinal { get; } = new(StringComparer.Ordinal);
    public static Utf8Comparer OrdinalIgnoreCase { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly IAlternateEqualityComparer<ReadOnlySpan<char>, string?> _comparer =
        (IAlternateEqualityComparer<ReadOnlySpan<char>, string?>)comparer;

    public bool Equals(StringLike x, StringLike y) => _comparer.Equals(x, y);

    public int GetHashCode(StringLike obj) => _comparer.GetHashCode(obj);

    public StringLike Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public bool Equals(ReadOnlySpan<byte> alternate, StringLike other)
    {
        return Utf8Util.WithBytes(
            alternate,
            (other: other.Value, @this: this),
            static (alternate, state) => state.@this._comparer.Equals(alternate, state.other)
        );
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        return Utf8Util.WithBytes(alternate, this, static (alternate, @this) => @this._comparer.GetHashCode(alternate));
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
