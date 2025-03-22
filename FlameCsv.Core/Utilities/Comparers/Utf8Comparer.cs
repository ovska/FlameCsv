using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FlameCsv.Utilities.Comparers;

internal sealed class Utf8Comparer
    : IEqualityComparer<StringLike>, IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>
{
    [field: AllowNull, MaybeNull] public static Utf8Comparer Ordinal => field ??= new(false);
    [field: AllowNull, MaybeNull] public static Utf8Comparer OrdinalIgnoreCase => field ??= new(true);

    private readonly bool _ignoreCase;

    private IAlternateEqualityComparer<ReadOnlySpan<char>, string?> Comparer
        => (
            IAlternateEqualityComparer<ReadOnlySpan<char>, string?>)(_ignoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

    private Utf8Comparer(bool ignoreCase) => _ignoreCase = ignoreCase;

    public bool Equals(StringLike x, StringLike y) => Comparer.Equals(x, y);
    public int GetHashCode(StringLike obj) => Comparer.GetHashCode(obj);

    [ExcludeFromCodeCoverage]
    public StringLike Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public bool Equals(ReadOnlySpan<byte> alternate, StringLike other)
    {
        return Utf8Util.WithBytes(
            alternate,
            (other: other.Value, @this: this),
            static (alternate, state) => state.@this.Comparer.Equals(alternate, state.other));
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        return Utf8Util.WithBytes(
            alternate,
            this,
            static (alternate, @this) => @this.Comparer.GetHashCode(alternate));
    }
}
