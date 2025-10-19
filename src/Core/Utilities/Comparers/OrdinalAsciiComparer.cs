using System.Text;
using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Reflection;

namespace FlameCsv.Utilities.Comparers;

internal sealed class OrdinalAsciiComparer
    : IEqualityComparer<StringLike>,
        IEqualityComparer<string>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, string>
{
    public static OrdinalAsciiComparer Instance { get; } = new();

    private OrdinalAsciiComparer() { }

    public bool Equals(StringLike x, StringLike y) => StringComparer.Ordinal.Equals(x, y);

    public bool Equals(ReadOnlySpan<byte> alternate, StringLike other) => Ascii.Equals(alternate, other);

    public StringLike Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public int GetHashCode(StringLike obj)
    {
        return Utf8Util.WithChars(obj, this, static (obj, state) => state.GetHashCode(obj));
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate) => HashCode<byte>.Combine(alternate);

    bool IAlternateEqualityComparer<ReadOnlySpan<byte>, string>.Equals(ReadOnlySpan<byte> alternate, string other)
    {
        return Equals(alternate, (StringLike)other);
    }

    string IAlternateEqualityComparer<ReadOnlySpan<byte>, string>.Create(ReadOnlySpan<byte> alternate)
    {
        return Create(alternate);
    }

    bool IEqualityComparer<string>.Equals(string? x, string? y) => StringComparer.Ordinal.Equals(x, y);

    int IEqualityComparer<string>.GetHashCode(string obj)
    {
        return GetHashCode((StringLike)obj);
    }
}
