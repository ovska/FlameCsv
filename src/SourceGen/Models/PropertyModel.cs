using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct  PropertyModel : IComparable<PropertyModel>, IMemberModel
{
    /// <summary>
    /// Property/field name, including a possible interface name, e.g. "ISomething_Prop"
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Actual name of the property/field, e.g. "Prop".
    /// </summary>
    /// <seealso cref="Identifier"/>.
    public required string HeaderName { get; init; }

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
    /// Whether the property is ignored when reading CSV.
    /// </summary>
    public required bool IsIgnored { get; init; }

    /// <summary>
    /// Order of the property/field.
    /// </summary>
    public required int? Order { get; init; }

    /// <summary>
    /// Index of the property/field to use in headerless CSV.
    /// </summary>
    public required int? Index { get; init; }

    /// <summary>
    /// If this member can be used when reading CSV, i.e., value can be written to the object.
    /// </summary>
    public required bool CanRead { get; init; }

    /// <summary>
    /// If this member can be used when writing CSV, i.e., value can be read from the object.
    /// </summary>
    public required bool CanWrite { get; init; }

    /// <summary>
    /// List of strings to match this member for. If empty, <see cref="HeaderName"/> should be used.
    /// </summary>
    public required EquatableArray<string> Aliases { get; init; }

    /// <summary>
    /// The fully qualified name of the interface type that this property was explicitly implemented from.
    /// </summary>
    public required string? ExplicitInterfaceOriginalDefinitionName { get; init; }

    /// <summary>
    /// Overridden converter for this property.
    /// </summary>
    public required ConverterModel? OverriddenConverter { get; init; }

    /// <summary>
    /// Whether the type has interfaces that support builtin conversion.
    /// </summary>
    public required BuiltinConvertable Convertability { get; init; }

    ModelKind IMemberModel.Kind => IsProperty ? ModelKind.Property : ModelKind.Field;

    public int CompareTo(PropertyModel other) => (Order ?? 0).CompareTo(other.Order ?? 0);

    public void WriteId(IndentedTextWriter writer)
    {
        writer.Write("@s__Id_");
        writer.Write(Identifier);
    }

    public void WriteConverterName(IndentedTextWriter writer)
    {
        writer.Write("@s__Converter_");
        writer.Write(Identifier);
    }

    public static PropertyModel? TryCreate(
        ITypeSymbol token,
        IPropertySymbol propertySymbol,
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
            in symbols,
            ref collector);

        return new PropertyModel
        {
            Type = new TypeRef(propertySymbol.Type),
            Identifier = explicitPropertyName ?? propertySymbol.Name,
            HeaderName = explicitPropertyOriginalName ?? propertySymbol.Name,
            IsProperty = true,
            IsIgnored = meta.IsIgnored,
            IsRequired =
                meta.IsRequired ||
                propertySymbol.IsRequired ||
                propertySymbol.SetMethod is { IsInitOnly: true },
            Aliases = meta.Aliases,
            Order = meta.Order,
            Index = meta.Index,
            CanRead = !propertySymbol.IsReadOnly && !meta.IsIgnored,
            CanWrite = !propertySymbol.IsWriteOnly && meta.IsIgnored is not true,
            OverriddenConverter
                = ConverterModel.Create(token, propertySymbol, propertySymbol.Type, in symbols, ref collector),
            ExplicitInterfaceOriginalDefinitionName
                = explicitInterface?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Convertability = propertySymbol.Type.GetBuiltinConvertability(in symbols),
        };
    }

    public static PropertyModel? TryCreate(
        ITypeSymbol token,
        IFieldSymbol fieldSymbol,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector)
    {
        // a lot of these are unspeakable backing fields
        if (!fieldSymbol.CanBeReferencedByName || fieldSymbol.RefKind != RefKind.None || fieldSymbol.IsConst)
        {
            return null;
        }

        SymbolMetadata meta = new(fieldSymbol.Name, fieldSymbol, in symbols, ref collector);

        return new PropertyModel
        {
            Type = new TypeRef(fieldSymbol.Type),
            IsProperty = false,
            IsRequired = meta.IsRequired || fieldSymbol.IsRequired,
            IsIgnored = meta.IsIgnored,
            Identifier = fieldSymbol.Name,
            HeaderName = fieldSymbol.Name,
            Aliases = meta.Aliases,
            Order = meta.Order,
            Index = meta.Index,
            CanRead = !fieldSymbol.IsReadOnly && !meta.IsIgnored,
            CanWrite = meta.IsIgnored is not true,
            ExplicitInterfaceOriginalDefinitionName = null,
            Convertability = fieldSymbol.Type.GetBuiltinConvertability(in symbols),
            OverriddenConverter = ConverterModel.Create(
                token,
                fieldSymbol,
                fieldSymbol.Type,
                in symbols,
                ref collector)
        };
    }

    public bool Equals(IMemberModel? other) => other is PropertyModel model && Equals(model);
}
