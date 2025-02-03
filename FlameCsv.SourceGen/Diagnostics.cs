using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen;

internal static class Diagnostics
{
    public static Diagnostic FileScopedType(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.FileScopedType,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic NoValidConstructor(ITypeSymbol type, IMethodSymbol? constructor)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoValidConstructor,
            location: GetLocation(constructor) ?? GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic RefConstructorParameter(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.RefConstructorParameter,
            location: GetLocation(parameter),
            additionalLocations: GetLocations(constructor),
            messageArgs: [type.ToDisplayString(), parameter.RefKind, parameter.ToDisplayString()]);
    }

    public static Diagnostic RefLikeConstructorParameter(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.RefLikeConstructorParameter,
            location: GetLocation(parameter),
            additionalLocations: GetLocations(constructor),
            messageArgs: [type.ToDisplayString(), parameter.ToDisplayString()]);
    }

    public static Diagnostic MultipleTypeProxies(ITypeSymbol targetType, List<ProxyData> proxies)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.MultipleTypeProxies,
            location: GetLocation(targetType),
            additionalLocations: proxies.Select(l => l.AttributeLocation).OfType<Location>(),
            messageArgs: targetType.ToDisplayString());
    }

    public static Diagnostic ExplicitInterfaceRequired(string propertyName, Location? attributeLocation)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.ExplicitInterfaceRequired,
            location: attributeLocation,
            messageArgs: [propertyName]);
    }

    public static Diagnostic NoReadableMembers(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoReadableMembers,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic NoWritableMembers(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoWritableMembers,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic NoCsvFactoryConstructor(ISymbol target, string factoryType, ITypeSymbol tokenType)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoCsvFactoryConstructor,
            location: GetLocation(target),
            messageArgs: [target.ToDisplayString(), factoryType, tokenType.ToDisplayString()]);
    }

    public static Diagnostic CsvConverterAbstract(ISymbol target, string converterType)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.CsvConverterAbstract,
            location: GetLocation(target),
            messageArgs: [converterType, target.ToDisplayString()]);
    }

    public static Diagnostic TargetMemberNotFound(
        ITypeSymbol targetType,
        Location? location,
        in TargetAttributeModel targetModel)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.TargetMemberNotFound,
            location: location ?? GetLocation(targetType),
            messageArgs:
            [
                targetModel.IsParameter ? "Parameter" : "Property/field",
                targetModel.MemberName,
                targetType.ToDisplayString(),
            ]);
    }

    private static Location? GetLocation(ISymbol? symbol)
    {
        if (symbol is not null)
        {
            foreach (var location in symbol.Locations)
            {
                if (location.IsInSource)
                {
                    return location;
                }
            }
        }

        return null;
    }

    private static IEnumerable<Location> GetLocations(ISymbol? symbol)
    {
        return symbol is null ? [] : symbol.Locations.Where(l => l.IsInSource);
    }

    internal static void CheckIfFileScoped(
        ITypeSymbol type,
        CancellationToken cancellationToken,
        ref List<Diagnostic>? diagnostics)
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax(cancellationToken) is not TypeDeclarationSyntax classDeclaration)
            {
                continue;
            }

            foreach (var modifier in classDeclaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.FileKeyword))
                {
                    (diagnostics ??= []).Add(FileScopedType(type));
                    return;
                }
            }
        }
    }
}
