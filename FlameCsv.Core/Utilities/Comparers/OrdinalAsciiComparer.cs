using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Helpers;

namespace FlameCsv.Utilities.Comparers;

internal sealed class OrdinalAsciiComparer
    : IEqualityComparer<StringLike>, IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>
{
    [field: AllowNull, MaybeNull] public static OrdinalAsciiComparer Instance => field ??= new();

    private OrdinalAsciiComparer()
    {
    }

    public bool Equals(StringLike x, StringLike y) => StringComparer.Ordinal.Equals(x, y);

    public bool Equals(ReadOnlySpan<byte> alternate, StringLike other) => Ascii.Equals(alternate, other);

    [ExcludeFromCodeCoverage]
    public StringLike Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public int GetHashCode(StringLike obj)
    {
        return Utf8Util.WithChars(
            obj,
            this,
            static (obj, state) => state.GetHashCode(obj));
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate) => HashCode<byte>.Combine(alternate);
}
