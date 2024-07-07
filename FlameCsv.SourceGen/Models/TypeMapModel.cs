using System;
using System.Collections.Generic;
using System.Text;
using FlameCsv.SourceGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen.Models;

internal static class Test
{

}

internal sealed record TypeMapModel
{
    public bool Invalid { get; set; }

    public TypeMapModel(INamedTypeSymbol containingClass, AttributeData attribute)
    {
        TypeMap = new TypeRef(containingClass);
        Token = new TypeRef(attribute.AttributeClass!.TypeArguments[0]);
        Type = new TypeRef(attribute.AttributeClass!.TypeArguments[1]);

        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key.Equals("Scope", StringComparison.Ordinal))
            {
                Scope = kvp.Value.Value switch
                {
                    BindingScope bs when bs is BindingScope.All or BindingScope.Write or BindingScope.Read => bs,
                    _ => throw new NotSupportedException("Unrecognized binding scope: " + kvp.Value.ToCSharpString()),
                };

                break;
            }
        }


    }

    /// <summary>
    /// TypeRef to the TypeMap object
    /// </summary>
    public TypeRef TypeMap { get; }

    /// <summary>
    /// Ref to the token type
    /// </summary>
    public TypeRef Token { get; }

    /// <summary>
    /// Ref to the converted type.
    /// </summary>
    public TypeRef Type { get; }

    public BindingScope Scope { get; }
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
        TypeMapModel typeMap,
        KnownSymbols knownSymbols,
        out PropertyModel model)
    {
        ITypeSymbol type;
        bool isProperty = false;
        TypeRef? explicitInterface = null;
        bool isRequired;
        Meta meta;

        if (symbol is IPropertySymbol propertySymbol)
        {
            if (!ValidProperty(propertySymbol))
                goto Fail;

            type = propertySymbol.Type;
            isProperty = true;
            isRequired = propertySymbol.IsRequired || propertySymbol.SetMethod is { IsInitOnly: true };

            if (propertySymbol.ContainingType.TypeKind == TypeKind.Interface &&
                !SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, typeSymbol))
            {
                explicitInterface = new TypeRef(propertySymbol.OriginalDefinition.ContainingType);
            }
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            if (!ValidField(fieldSymbol))
                goto Fail;

            type = fieldSymbol.Type;
            isRequired = fieldSymbol.IsRequired;
        }
        else
        {
            goto Fail;
        }

        meta = GetMetadata(symbol, knownSymbols);

        model = new PropertyModel
        {
            Type = new TypeRef(type),
            IsProperty = isProperty,
            IsRequired = isRequired || meta.IsRequired,
            Name = symbol.Name,
            ExplicitInterfaceOriginalDefinition = explicitInterface,
            Names = meta.Names.ToImmutableEquatableArray(sorted: true),
            Order = meta.Order,
            Scope = meta.Scope,
        };
        return true;

        Fail:
        model = null!;
        return false;

        bool ValidField(IFieldSymbol f)
        {
            if (!f.CanBeReferencedByName ||
                f.IsStatic ||
                f.RefKind != RefKind.None ||
                f.HasAttribute(knownSymbols.CsvHeaderIgnoreAttribute))
            {
                return false;
            }

            // either field must be writable, or we are generating writing code too
            return typeMap.Scope != BindingScope.Read || (!f.IsReadOnly && !f.IsConst);
        }

        bool ValidProperty(IPropertySymbol p)
        {
            if (!p.CanBeReferencedByName ||
                p.IsStatic ||
                p.IsIndexer ||
                p.RefKind != RefKind.None ||
                p.HasAttribute(knownSymbols.CsvHeaderIgnoreAttribute))
            {
                return false;
            }

            return typeMap.Scope switch
            {
                BindingScope.Read => !p.IsReadOnly, // only reading code, must be writable
                BindingScope.Write => !p.IsWriteOnly, // only writing code, must be readable
                _ => true,
            };
        }
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
                        names[i] = namesArray[i].Value as string ?? "";
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

internal sealed record ParameterModel
{
    public required TypeRef ParameterType { get; init; }
    public required string Name { get; init; }
    public required bool HasDefaultValue { get; init; }

    // The default value of a constructor parameter can only be a constant
    // so it always satisfies the structural equality requirement for the record.
    public required object? DefaultValue { get; init; }
    public required int ParameterIndex { get; init; }

    public static bool TryCreate(ISymbol symbol, out ParameterModel model)
    {
        if (symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } ctor)
            goto Fail;




        Fail:
        model = default!;
        return false;
    }
}
