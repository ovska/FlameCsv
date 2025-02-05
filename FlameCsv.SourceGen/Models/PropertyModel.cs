using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record PropertyModel : IComparable<PropertyModel>, IMemberModel
{
    /// <summary>
    /// Property/field name, including a possible interface name, e.g. "ISomething_Prop"
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Actual name of the property/field, e.g. "Prop".
    /// </summary>
    /// <seealso cref="Identifier"/>.
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
    /// List of strings to match this member for. If empty, <see cref="Name"/> should be used.
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

    public void WriteId(StringBuilder sb)
    {
        sb.Append("@s__Id_");
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
        CancellationToken cancellationToken,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector)
    {
        if (propertySymbol.IsIndexer || propertySymbol.RefKind != RefKind.None)
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

        SymbolMetadata meta = new(
            explicitPropertyOriginalName ?? propertySymbol.Name,
            propertySymbol,
            cancellationToken,
            in symbols,
            ref collector);

        if (explicitPropertyName is not null && meta.IsRequired is true)
        {
            collector.AddDiagnostic(
                Diagnostics.ExplicitInterfaceRequired(
                    propertySymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    meta.GetLocation(cancellationToken)));
        }

        return new PropertyModel
        {
            Type = new TypeRef(propertySymbol.Type),
            Identifier = explicitPropertyName ?? propertySymbol.Name,
            Name = explicitPropertyOriginalName ?? propertySymbol.Name,
            IsProperty = true,
            IsRequired
                = meta.IsRequired is true ||
                propertySymbol.IsRequired ||
                propertySymbol is { SetMethod.IsInitOnly: true },
            Names = meta.Names,
            Order = meta.Order ?? 0,
            CanRead = !propertySymbol.IsReadOnly,
            CanWrite = !propertySymbol.IsWriteOnly,
            OverriddenConverter
                = ConverterModel.Create(token, propertySymbol, propertySymbol.Type, in symbols, ref collector),
            ExplicitInterfaceOriginalDefinitionName
                = explicitInterface?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        };
    }

    public static PropertyModel? TryCreate(
        ITypeSymbol token,
        IFieldSymbol fieldSymbol,
        CancellationToken cancellationToken,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector)
    {
        // a lot of these are unspeakable backing fields
        if (!fieldSymbol.CanBeReferencedByName || fieldSymbol.RefKind != RefKind.None || fieldSymbol.IsConst)
        {
            return null;
        }

        SymbolMetadata meta = new(fieldSymbol.Name, fieldSymbol, cancellationToken, in symbols, ref collector);

        return new PropertyModel
        {
            Type = new TypeRef(fieldSymbol.Type),
            IsProperty = false,
            IsRequired = meta.IsRequired is true || fieldSymbol.IsRequired,
            Identifier = fieldSymbol.Name,
            Name = fieldSymbol.Name,
            Names = meta.Names,
            Order = meta.Order ?? 0,
            CanRead = !fieldSymbol.IsReadOnly,
            CanWrite = true,
            ExplicitInterfaceOriginalDefinitionName = null,
            OverriddenConverter = ConverterModel.Create(
                token,
                fieldSymbol,
                fieldSymbol.Type,
                in symbols,
                ref collector)
        };
    }

    public bool Equals(IMemberModel? other) => Equals(other as PropertyModel);
}
