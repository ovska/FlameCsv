using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record RecordModel
{

}

internal sealed record PropertyModel : IComparable<PropertyModel>
{
    /// <summary>
    /// Property/field name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Property/field type
    /// </summary>
    public required TypeRef Type { get; init; }

    /// <summary>
    /// Whether this is a property and not a field.
    /// </summary>
    public required bool IsProperty { get; init; }

    /// <summary>
    /// Whether the property/field is required when reading CSV.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Order of the property/field.
    /// </summary>
    public required int Order { get; init; }

    /// <summary>
    /// Scope this property/field is valid for.
    /// </summary>
    public required BindingScope Scope { get; init; }

    /// <summary>
    /// If this member can be used when reading CSV.
    /// </summary>
    public required bool CanRead { get; init; }

    /// <summary>
    /// If this member can be used when writing CSV.
    /// </summary>
    public required bool CanWrite { get; init; }

    /// <summary>
    /// List of strings to match this member for. Defaults to <see cref="Name"/>
    /// </summary>
    public required ImmutableEquatableArray<string> Names { get; init; }

    /// <summary>
    /// The interface type that this property was explicitly implemented from.
    /// </summary>
    public required TypeRef? ExplicitInterfaceOriginalDefinition { get; init; }

    public int CompareTo(PropertyModel other) => other.Order.CompareTo(Order); // reverse sort so higher order is first

    public static bool TryCreate(
        ISymbol typeSymbol,
        ISymbol symbol,
        KnownSymbols knownSymbols,
        [NotNullWhen(true)] out PropertyModel? model)
    {
        if (!symbol.CanBeReferencedByName || symbol.IsStatic)
            goto Fail;

        ITypeSymbol type;
        bool isProperty = false;
        TypeRef? explicitInterface = null;
        bool isRequired;
        bool canWrite;
        bool canRead;

        if (symbol is IPropertySymbol propertySymbol)
        {
            if (propertySymbol.IsIndexer ||
                propertySymbol.RefKind != RefKind.None ||
                propertySymbol.HasAttribute(knownSymbols.CsvHeaderIgnoreAttribute))
            {
                goto Fail;
            }

            type = propertySymbol.Type;
            isProperty = true;
            isRequired = propertySymbol.IsRequired || propertySymbol.SetMethod is { IsInitOnly: true };
            canWrite = !propertySymbol.IsWriteOnly;
            canRead = !propertySymbol.IsReadOnly;

            if (propertySymbol.ContainingType.TypeKind == TypeKind.Interface &&
                !SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, typeSymbol))
            {
                explicitInterface = new TypeRef(propertySymbol.OriginalDefinition.ContainingType);
            }
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            if (fieldSymbol.RefKind != RefKind.None ||
                fieldSymbol.IsConst || // should this be writable?
                fieldSymbol.HasAttribute(knownSymbols.CsvHeaderIgnoreAttribute))
            {
                goto Fail;
            }

            type = fieldSymbol.Type;
            isRequired = fieldSymbol.IsRequired;
            canWrite = true;
            canRead = !fieldSymbol.IsReadOnly;
        }
        else
        {
            goto Fail;
        }

        Meta meta = GetMetadata(symbol, knownSymbols);

        model = new PropertyModel
        {
            Type = new TypeRef(type),
            IsProperty = isProperty,
            IsRequired = isRequired || meta.IsRequired,
            Name = symbol.Name,
            ExplicitInterfaceOriginalDefinition = explicitInterface,
            Names = meta.Names.ToImmutableEquatableArray(),
            Order = meta.Order,
            Scope = meta.Scope,
            CanRead = meta.Scope != BindingScope.Write && canRead,
            CanWrite = meta.Scope != BindingScope.Read && canWrite,
        };
        return true;

        Fail:
        model = null;
        return false;
    }

    private record struct Meta(string[] Names, int Order, bool IsRequired, BindingScope Scope);

    private static Meta GetMetadata(ISymbol member, KnownSymbols knownSymbols)
    {
        string[]? names = null;
        bool isRequired = false;
        int order = 0;
        BindingScope scope = BindingScope.All;

        foreach (var attributeData in member.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, knownSymbols.CsvHeaderAttribute))
            {
                // params-array
                if (attributeData.ConstructorArguments[0].Values is { IsDefaultOrEmpty: false } namesArray)
                {
                    names = new string[namesArray.Length];

                    for (int i = 0; i < namesArray.Length; i++)
                        names[i] = namesArray[i].Value?.ToString() ?? "";
                }

                foreach (var argument in attributeData.NamedArguments)
                {
                    switch (argument)
                    {
                        case { Key: "Required", Value.Value: bool _required }:
                            isRequired = _required;
                            break;
                        case { Key: "Order", Value.Value: int _order }:
                            order = _order;
                            break;
                        case { Key: "Scope", Value.Value: int _scope }:
                            scope = (BindingScope)_scope;
                            break;
                    }
                }

                break;
            }
        }

        return new Meta(names ?? [member.Name], order, isRequired, scope);
    }
}
