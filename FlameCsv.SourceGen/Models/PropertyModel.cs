using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record PropertyModel : IComparable<PropertyModel>, IMemberModel
{
    /// <summary>
    /// Property/field name, including a possible interface name.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Actual name of the property/field, e.g. "Prop" if explicitly implemented via ISomething.Prop.
    /// Otherwise, same as <see cref="Identifier"/>.
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
    /// If this member can be used when reading CSV, i.e., value can be written to the object.
    /// </summary>
    public required bool CanRead { get; init; }

    /// <summary>
    /// If this member can be used when writing CSV, i.e., value can be read from the object.
    /// </summary>
    public required bool CanWrite { get; init; }

    /// <summary>
    /// List of strings to match this member for. Defaults to <see cref="Name"/>
    /// </summary>
    public required EquatableArray<string> Names { get; init; }

    /// <summary>
    /// The fully qualified name of the interface type that this property was explicitly implemented from.
    /// </summary>
    public required string? ExplicitInterfaceOriginalDefinitionName { get; init; }

    /// <summary>
    /// Overridden converter for this property.
    /// </summary>
    public required ConverterModel? OverriddenConverter { get; init; }

    public int CompareTo(PropertyModel other) => other.Order.CompareTo(Order); // reverse sort so higher order is first

    public void WriteIndexName(StringBuilder sb)
    {
        sb.Append("@s__Index_");
        sb.Append(Identifier);
    }

    public void WriteConverterName(StringBuilder sb)
    {
        sb.Append("@s__Converter_");
        sb.Append(Identifier);
    }

    public static PropertyModel? TryCreate(
        ITypeSymbol token,
        IPropertySymbol propertySymbol,
        ref readonly FlameSymbols symbols,
        CancellationToken cancellationToken,
        ref List<Diagnostic>? diagnostics)
    {
        if (propertySymbol.IsIndexer || propertySymbol.RefKind != RefKind.None)
        {
            return null;
        }

        SymbolMetadata meta = new(propertySymbol, in symbols);

        if (meta.IsIgnored)
        {
            return null;
        }

        INamedTypeSymbol? explicitInterface = null;
        string? explicitPropertyName = null;
        string? explicitPropertyOriginalName = null;

        foreach (var explicitImplementation in propertySymbol.ExplicitInterfaceImplementations)
        {
            var originalDefinition = explicitImplementation.OriginalDefinition;

            if (originalDefinition.CanBeReferencedByName)
            {
                explicitPropertyName = $"{originalDefinition.ContainingType.Name}_{originalDefinition.Name}";
                explicitPropertyOriginalName = originalDefinition.Name;
                explicitInterface = originalDefinition.ContainingType;
                break;
            }
        }

        if (!propertySymbol.CanBeReferencedByName && explicitInterface is null)
        {
            // cannot reference by name and not an explicit interface implementation
            return null;
        }

        if (explicitPropertyName is not null && meta.IsRequired)
        {
            (diagnostics ??= []).Add(
                Diagnostics.ExplicitInterfaceRequired(propertySymbol, meta.GetLocation(cancellationToken)));
        }

        return new PropertyModel
        {
            Type = new TypeRef(propertySymbol.Type),
            Identifier = explicitPropertyName ?? propertySymbol.Name,
            Name = explicitPropertyOriginalName ?? propertySymbol.Name,
            IsProperty = true,
            IsRequired
                = meta.IsRequired || propertySymbol.IsRequired || propertySymbol is { SetMethod.IsInitOnly: true },
            Names = meta.Names,
            Order = meta.Order,
            CanRead = !propertySymbol.IsReadOnly,
            CanWrite = !propertySymbol.IsWriteOnly,
            OverriddenConverter = ConverterModel.Create(token, propertySymbol, propertySymbol.Type, in symbols),
            ExplicitInterfaceOriginalDefinitionName
                = explicitInterface?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        };
    }

    public static PropertyModel? TryCreate(
        ITypeSymbol token,
        IFieldSymbol fieldSymbol,
        ref readonly FlameSymbols symbols)
    {
        // a lot of these are unspeakable backing fields
        if (!fieldSymbol.CanBeReferencedByName || fieldSymbol.RefKind != RefKind.None || fieldSymbol.IsConst)
        {
            return null;
        }

        SymbolMetadata meta = new(fieldSymbol, in symbols);

        if (meta.IsIgnored)
        {
            return null;
        }

        return new PropertyModel
        {
            Type = new TypeRef(fieldSymbol.Type),
            IsProperty = false,
            IsRequired = meta.IsRequired || fieldSymbol.IsRequired,
            Identifier = fieldSymbol.Name,
            Name = fieldSymbol.Name,
            Names = meta.Names,
            Order = meta.Order,
            CanRead = !fieldSymbol.IsReadOnly,
            CanWrite = true,
            ExplicitInterfaceOriginalDefinitionName = null,
            OverriddenConverter = ConverterModel.Create(token, fieldSymbol, fieldSymbol.Type, in symbols)
        };
    }

    public bool Equals(IMemberModel other) => Equals(other as PropertyModel);
}
