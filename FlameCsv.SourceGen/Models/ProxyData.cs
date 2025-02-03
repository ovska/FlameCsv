namespace FlameCsv.SourceGen.Models;

/// <summary>
/// Contains the type and location of a proxy type.
/// </summary>
/// <param name="type">Typeref to the created proxy</param>
/// <param name="attributeLocation">Location of the attribute that configured this type</param>
internal readonly struct ProxyData(TypeRef type, Location? attributeLocation) : IEquatable<ProxyData>
{
    public TypeRef Type { get; } = type;
    public Location? AttributeLocation { get; } = attributeLocation;

    // ensure this doesn't slip into any equality checks
    public bool Equals(ProxyData _) => throw new NotSupportedException();
    public override int GetHashCode() => throw new NotSupportedException();
}
