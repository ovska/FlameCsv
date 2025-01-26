using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record PropertyModel : IComparable<PropertyModel>, IMemberModel
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
    public required CsvBindingScope Scope { get; init; }

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

    /// <summary>
    /// Overridden converter for this property.
    /// </summary>
    public required ConverterModel? OverriddenConverter { get; init; }

    public string IndexPrefix => "s__Index_";
    public string ConverterPrefix => "s__Converter_";

    public int CompareTo(PropertyModel other) => other.Order.CompareTo(Order); // reverse sort so higher order is first

    public static bool TryCreate(
        ITypeSymbol token,
        ISymbol typeSymbol,
        ISymbol symbol,
        FlameSymbols symbols,
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
                propertySymbol.HasAttribute(symbols.CsvHeaderIgnoreAttribute))
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
                fieldSymbol.IsConst ||
                fieldSymbol.HasAttribute(symbols.CsvHeaderIgnoreAttribute))
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

        var meta = new SymbolMetadata(token, symbol, symbols);

        model = new PropertyModel
        {
            Type = new TypeRef(type),
            IsProperty = isProperty,
            IsRequired = isRequired || meta.IsRequired,
            Name = symbol.Name,
            ExplicitInterfaceOriginalDefinition = explicitInterface,
            Names = meta.Names.ToImmutableUnsortedArray(),
            Order = meta.Order,
            Scope = meta.Scope,
            CanRead = meta.Scope != CsvBindingScope.Write && canRead,
            CanWrite = meta.Scope != CsvBindingScope.Read && canWrite,
            OverriddenConverter = ConverterModel.GetOverriddenConverter(token, symbol, type, symbols)
        };
        return true;

    Fail:
        model = null;
        return false;
    }
}
