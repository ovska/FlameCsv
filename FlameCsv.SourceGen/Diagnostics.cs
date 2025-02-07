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
            location: GetLocation(constructor, type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic RefConstructorParameter(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.RefConstructorParameter,
            location: GetLocation(parameter, constructor),
            additionalLocations: constructor?.Locations.Where(l => l.IsInSource),
            messageArgs: [type.ToDisplayString(), parameter.RefKind, parameter.ToDisplayString()]);
    }

    public static Diagnostic RefLikeConstructorParameter(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.RefLikeConstructorParameter,
            location: GetLocation(parameter, constructor, type),
            additionalLocations: constructor?.Locations.Where(l => l.IsInSource),
            messageArgs: [type.ToDisplayString(), parameter.ToDisplayString()]);
    }

    public static Diagnostic MultipleTypeProxies(ITypeSymbol targetType, ICollection<Location?> locations)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.MultipleTypeProxies,
            location: locations.FirstOrDefault() ?? GetLocation(targetType),
            additionalLocations: locations.OfType<Location>(),
            messageArgs: targetType.ToDisplayString());
    }

    public static Diagnostic ExplicitInterfaceRequired(string propertyName, Location? attributeLocation)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.ExplicitInterfaceRequired,
            location: attributeLocation,
            messageArgs: [propertyName]);
    }

    public static Diagnostic ConflictingConfiguration(
        ITypeSymbol targetType,
        string memberType,
        string memberName,
        string configurationName,
        Location? location,
        Location? additionalLocation)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.ConflictingConfiguration,
            location: location ?? additionalLocation ?? GetLocation(targetType),
            additionalLocations: additionalLocation is not null ? [additionalLocation] : null,
            messageArgs: [memberType, memberName, targetType.ToDisplayString(), configurationName]);
    }

    public static Diagnostic IgnoredParameterWithoutDefaultValue(IParameterSymbol parameter, ITypeSymbol targetType)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.IgnoredParameterWithoutDefaultValue,
            location: GetLocation(parameter, parameter.ContainingSymbol, targetType),
            messageArgs: [parameter.ToDisplayString(), targetType.ToDisplayString()]);
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

    public static Diagnostic NoMatchingConstructor(
        ITypeSymbol targetType,
        IEnumerable<ITypeSymbol?> types,
        Location? location)
    {
        return Diagnostic.Create(
            descriptor: Descriptors.NoMatchingConstructor,
            location: location ?? GetLocation(targetType),
            messageArgs:
            [
                targetType.ToDisplayString(),
                string.Join(", ", types.Select(t => t?.ToDisplayString() ?? "<unknown>")),
            ]);
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

    /// <summary>
    /// Returns the first valid source location from the given symbols, checked in order.
    /// </summary>
    private static Location? GetLocation(params ReadOnlySpan<ISymbol?> symbols)
    {
        foreach (var symbol in symbols)
        {
            if (symbol is null) continue;

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

    internal static void CheckIfFileScoped(
        ITypeSymbol type,
        CancellationToken cancellationToken,
        ref AnalysisCollector collector)
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
                    collector.AddDiagnostic(FileScopedType(type));
                    return;
                }
            }
        }
    }
}
