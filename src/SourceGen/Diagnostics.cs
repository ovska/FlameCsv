using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen;

internal static class Diagnostics
{
    public static Diagnostic NotPartialType(ITypeSymbol type, ITypeSymbol? generationTarget, Location? location)
    {
        string targetMessage = "";

        if (generationTarget is not null)
        {
            targetMessage += $" of type {type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}";

            if (!generationTarget.Locations.IsDefaultOrEmpty)
            {
                targetMessage += $" (line {generationTarget.Locations[0].GetLineSpan().StartLinePosition.Line + 1})";
            }
        }

        return Diagnostic.Create(
            descriptor: Descriptors.NotPartialType,
            location: location ?? GetLocation(type),
            messageArgs: [type.ToDisplayString(), targetMessage]
        );
    }

    public static Diagnostic FileScopedType(ITypeSymbol type, Location? location)
    {
        // we don't need to specify the location of the attribute targeting this type,
        // as a file scoped type can only be defined in the same file as where the attribute is applied
        return Diagnostic.Create(
            descriptor: Descriptors.FileScopedType,
            location: location ?? GetLocation(type),
            messageArgs: type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        );
    }

    public static Diagnostic NoValidConstructor(ITypeSymbol type, ITypeSymbol actualType, IMethodSymbol? constructor)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoValidConstructor,
            location: GetLocation(constructor, actualType),
            messageArgs: type.ToDisplayString()
        );
    }

    public static Diagnostic RefConstructorParameter(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.RefConstructorParameter,
            location: parameter.GetFullLocation() ?? GetLocation(constructor, parameter),
            additionalLocations: constructor?.Locations.Where(l => l.IsInSource),
            messageArgs: [type.ToDisplayString(), parameter.ToDisplayString()]
        );
    }

    public static Diagnostic RefLikeConstructorParameter(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.RefLikeConstructorParameter,
            location: parameter.GetFullLocation() ?? GetLocation(parameter, constructor, type),
            additionalLocations: constructor?.Locations.Where(l => l.IsInSource),
            messageArgs: [type.ToDisplayString(), parameter.ToDisplayString()]
        );
    }

    public static Diagnostic MultipleTypeProxies(ITypeSymbol targetType, ICollection<Location?> locations)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.MultipleTypeProxies,
            location: locations.FirstOrDefault() ?? GetLocation(targetType),
            additionalLocations: locations.OfType<Location>(),
            messageArgs: targetType.ToDisplayString()
        );
    }

    public static Diagnostic NoTargetTypeOnAssembly(AttributeData attribute, ITypeSymbol attributeClass)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoTargetTypeOnAssembly,
            location: attribute.GetLocation() ?? GetLocation(attributeClass),
            messageArgs: [attributeClass.ToDisplayString()]
        );
    }

    public static Diagnostic NoMemberNameOnAttribute(AttributeData attribute, bool isOnAssembly)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoMemberNameOnAttribute,
            location: attribute.GetLocation() ?? GetLocation(attribute.AttributeClass),
            messageArgs:
            [
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "<unknown>",
                isOnAssembly ? "assembly" : "type",
            ]
        );
    }

    public static Diagnostic ConflictingConfiguration(
        ITypeSymbol targetType,
        string memberType,
        string memberName,
        string configurationName,
        Location? location
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.ConflictingConfiguration,
            location: location ?? GetLocation(targetType),
            messageArgs: [memberType, memberName, targetType.ToDisplayString(), configurationName]
        );
    }

    public static Diagnostic IgnoredParameterWithoutDefaultValue(IParameterSymbol parameter, ITypeSymbol targetType)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.IgnoredParameterWithoutDefaultValue,
            location: parameter.GetFullLocation() ?? GetLocation(parameter, parameter.ContainingSymbol, targetType),
            messageArgs: [parameter.ToDisplayString(), targetType.ToDisplayString()]
        );
    }

    public static Diagnostic NoReadableMembers(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoReadableMembers,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString()
        );
    }

    public static Diagnostic NoWritableMembers(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoWritableMembers,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString()
        );
    }

    public static Diagnostic NoCsvConverterConstructor(ISymbol target, string factoryType, ITypeSymbol tokenType)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoCsvConverterConstructor,
            location: GetLocation(target),
            messageArgs: [target.ToDisplayString(), factoryType, tokenType.ToDisplayString()]
        );
    }

    public static Diagnostic CsvConverterTypeMismatch(
        ISymbol target,
        ISymbol converterType,
        ITypeSymbol expectedType,
        ITypeSymbol actualType,
        Location? location
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.CsvConverterTypeMismatch,
            location: location ?? GetLocation(target),
            messageArgs:
            [
                converterType.ToDisplayString(),
                target.ToDisplayString(),
                expectedType.ToDisplayString(),
                actualType.ToDisplayString(),
            ]
        );
    }

    public static Diagnostic CsvConverterAbstract(ISymbol target, string converterType)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.CsvConverterAbstract,
            location: GetLocation(target),
            messageArgs: [converterType, target.ToDisplayString()]
        );
    }

    public static Diagnostic NoMatchingConstructor(
        ITypeSymbol targetType,
        IEnumerable<ITypeSymbol?> types,
        Location? location
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoMatchingConstructor,
            location: location ?? GetLocation(targetType),
            messageArgs:
            [
                targetType.ToDisplayString(),
                string.Join(", ", types.Select(t => t?.ToDisplayString() ?? "<unknown>")),
            ]
        );
    }

    public static Diagnostic TargetMemberNotFound(
        ITypeSymbol targetType,
        ref readonly AttributeConfiguration configuration
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.TargetMemberNotFound,
            location: configuration.Attribute.GetLocation() ?? GetLocation(targetType),
            messageArgs:
            [
                configuration.IsParameter ? "Parameter" : "Property/field",
                configuration.MemberName,
                targetType.ToDisplayString(),
            ]
        );
    }

    public static Diagnostic ConflictingIndex(ITypeSymbol targetType, string memberName)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.ConflictingIndex,
            location: GetLocation(targetType),
            messageArgs: [memberName, targetType.ToDisplayString()]
        );
    }

    public static Diagnostic GapInIndex(ITypeSymbol targetType, int index)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.GapInIndex,
            location: GetLocation(targetType),
            messageArgs: [index.ToString(), targetType.ToDisplayString()]
        );
    }

    /**/

    public static Diagnostic EnumUnsupportedToken(ISymbol targetType, AttributeData attribute, ITypeSymbol tokenType)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.EnumUnsupportedToken,
            location: attribute.GetLocation() ?? GetLocation(targetType),
            messageArgs: tokenType.ToDisplayString()
        );
    }

    public static Diagnostic EnumInvalidExplicitName(
        ISymbol enumSymbol,
        IFieldSymbol fieldSymbol,
        Location? location,
        string explicitName
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.EnumInvalidExplicitName,
            location: location ?? GetLocation(fieldSymbol, enumSymbol),
            messageArgs: [explicitName, enumSymbol.Name, fieldSymbol.Name]
        );
    }

    public static Diagnostic EnumDuplicateName(
        ISymbol enumSymbol,
        string memberName,
        Location? location,
        string explicitName
    )
    {
        return Diagnostic.Create(
            descriptor: Descriptors.EnumDuplicateName,
            location: location ?? GetLocation(enumSymbol),
            messageArgs: [explicitName, enumSymbol.Name, memberName]
        );
    }

    public static Diagnostic EnumUnsupportedFlag(ISymbol enumSymbol, string memberName, Location? location)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.EnumUnsupportedFlag,
            location: location ?? GetLocation(enumSymbol),
            messageArgs: [enumSymbol.Name, memberName]
        );
    }

    /// <summary>
    /// Returns the first valid non-empty source location from the given symbols, checked in order.
    /// </summary>
    private static Location? GetLocation(params ReadOnlySpan<ISymbol?> symbols)
    {
        foreach (var symbol in symbols)
        {
            if (symbol is null)
                continue;

            foreach (var location in symbol.Locations)
            {
                if (location.IsInSource && !location.SourceSpan.IsEmpty)
                {
                    return location;
                }
            }
        }

        return null;
    }

    internal static void EnsurePartial(
        ITypeSymbol type,
        CancellationToken cancellationToken,
        List<Diagnostic> diagnostics,
        ITypeSymbol? generationTarget = null
    )
    {
        MemberDeclarationSyntax? firstDeclaration = null;

        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax(cancellationToken) is not MemberDeclarationSyntax declarationSyntax)
            {
                continue;
            }

            firstDeclaration ??= declarationSyntax;

            foreach (var modifier in declarationSyntax.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    return;
                }
            }
        }

        Location? location = null;

        // add the diagnostic to the modifiers, unless the type has none; in that case, add it to the type itself
        if (firstDeclaration is { Modifiers.Span.IsEmpty: false })
        {
            location = Location.Create(firstDeclaration.SyntaxTree, firstDeclaration.Modifiers.Span);
        }

        diagnostics.Add(NotPartialType(type, generationTarget, location));
    }

    internal static void CheckIfFileScoped(
        ITypeSymbol type,
        CancellationToken cancellationToken,
        List<Diagnostic> diagnostics
    )
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax(cancellationToken) is not MemberDeclarationSyntax declarationSyntax)
            {
                continue;
            }

            foreach (var modifier in declarationSyntax.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.FileKeyword))
                {
                    diagnostics.Add(FileScopedType(type, modifier.GetLocation()));
                    return;
                }
            }
        }
    }

    private static Location? GetFullLocation(this IParameterSymbol symbol)
    {
        return symbol
            .DeclaringSyntaxReferences.Select(r => r.GetSyntax())
            .OfType<ParameterSyntax>()
            .Select(s => s.GetLocation())
            .FirstOrDefault();
    }
}
