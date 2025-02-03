using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen;

#pragma warning disable RS2001 // TODO: fix

internal static class Diagnostics
{
    // 1XX - general diagnostics
    // 2XX - invalid configuration through attributes
    // 3XX - invalid type configuration

    private static readonly DiagnosticDescriptor _fileScoped = new(
        id: "FLAMESG001",
        title: "File-scoped type",
        messageFormat: "File-scoped type {0} cannot participate in source generation",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _noCtor = new(
        id: "FLAMESG100",
        title: "No usable constructor found",
        messageFormat:
        "Cannot generate reading code: Could not find a valid public constructor for {0}: must have a constructor with [CsvConstructor], a single public constructor, or a parameterless constructor",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _refCtorParam = new(
        id: "FLAMESG101",
        title: "Invalid constructor parameter ref kind",
        messageFormat: "Cannot generate reading code: {0} had a constructor parameter with invalid ref kind {1}: {2}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _refStructParam = new(
        id: "FLAMESG102",
        title: "Constructor had a ref-like parameter",
        messageFormat: "Cannot generate reading code: {0} had a ref-like constructor parameter: {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _multipleProxies = new(
        id: "FLAMESG103",
        title: "Multiple type proxies",
        messageFormat: "Cannot generate reading code: Multiple type proxies found for {0}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _explicitInterfaceRequired = new(
        id: "FLAMESG104",
        title: "Required explicit interface implementation",
        messageFormat:
        "Cannot generate reading code: Explicitly implemented property {0} cannot be marked as required",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _noReadableMembers = new(
        id: "FLAMESG200",
        title: "No valid members/parameters for reading",
        messageFormat:
        "Cannot generate reading code: {0} had no properties, fields, or parameters that support writing their value",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _noWritableMembers = new(
        id: "FLAMESG201",
        title: "No valid members for writing",
        messageFormat: "Cannot generate writing code: {0} had no properties or fields that support reading their value",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _factoryNoCtorFound = new(
        id: "FLAMESG202",
        title: "CsvConverterFactory with no valid constructor",
        messageFormat:
        "Overridden converter factory type {0} on {1} must have an empty public constructor, or a constructor accepting CsvOptions<{2}>",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _converterAbstract = new(
        id: "FLAMESG203",
        title: "CsvConverter must not be abstract",
        messageFormat: "CsvConverter {0} for {1} must not be abstract",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _targetMemberNotFound = new(
        id: "FLAMESG204",
        title: "Target member not found",
        messageFormat: "{0} '{1}' not found on type {2}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static Diagnostic FileScopedType(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: _fileScoped,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic NoConstructorFound(ITypeSymbol type, IMethodSymbol? constructor)
    {
        return Diagnostic.Create(
            descriptor: _noCtor,
            location: GetLocation(constructor) ?? GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic RefConstructorParameterFound(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter)
    {
        return Diagnostic.Create(
            descriptor: _refCtorParam,
            location: GetLocation(parameter),
            additionalLocations: GetLocations(constructor),
            messageArgs: [type.ToDisplayString(), parameter.RefKind, parameter.ToDisplayString()]);
    }

    public static Diagnostic RefLikeConstructorParameterFound(
        ITypeSymbol type,
        IMethodSymbol constructor,
        IParameterSymbol parameter)
    {
        return Diagnostic.Create(
            descriptor: _refStructParam,
            location: GetLocation(parameter),
            additionalLocations: GetLocations(constructor),
            messageArgs: [type.ToDisplayString(), parameter.ToDisplayString()]);
    }

    public static Diagnostic MultipleTypeProxiesFound(ITypeSymbol targetType, List<ProxyData> proxies)
    {
        return Diagnostic.Create(
            descriptor: _multipleProxies,
            location: GetLocation(targetType),
            additionalLocations: proxies.Select(l => l.AttributeLocation).OfType<Location>(),
            messageArgs: targetType.ToDisplayString());
    }

    public static Diagnostic ExplicitInterfaceRequired(ISymbol property, Location? attributeLocation)
    {
        return Diagnostic.Create(
            descriptor: _explicitInterfaceRequired,
            location: attributeLocation ?? GetLocation(property),
            messageArgs: [property.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)]);
    }

    public static Diagnostic NoReadableMembers(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: _noReadableMembers,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic NoWritableMembers(ITypeSymbol type)
    {
        return Diagnostic.Create(
            descriptor: _noWritableMembers,
            location: GetLocation(type),
            messageArgs: type.ToDisplayString());
    }

    public static Diagnostic NoCsvFactoryConstructorFound(ISymbol target, string factoryType, ITypeSymbol tokenType)
    {
        return Diagnostic.Create(
            descriptor: _factoryNoCtorFound,
            location: GetLocation(target),
            messageArgs: [target.ToDisplayString(), factoryType, tokenType.ToDisplayString()]);
    }

    public static Diagnostic CsvConverterAbstract(ISymbol target, string converterType)
    {
        return Diagnostic.Create(
            descriptor: _converterAbstract,
            location: GetLocation(target),
            messageArgs: [converterType, target.ToDisplayString()]);
    }

    public static Diagnostic TargetMemberNotFound(
        ITypeSymbol targetType,
        Location? location,
        in TargetAttributeModel targetModel)
    {
        return Diagnostic.Create(
            descriptor: _targetMemberNotFound,
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
