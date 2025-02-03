namespace FlameCsv.SourceGen.Models;

internal readonly struct ProxyData(TypeRef type, Location? attributeLocation) : IEquatable<ProxyData>
{
    public TypeRef Type { get; } = type;
    public Location? AttributeLocation { get; } = attributeLocation;

    public bool Equals(ProxyData _) => throw new NotSupportedException();
    public override int GetHashCode() => throw new NotSupportedException();
}
