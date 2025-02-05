namespace FlameCsv.SourceGen.Models;

internal static class TypeAttribute
{
    public static void Parse(
        AttributeData attribute,
        CancellationToken cancellationToken,
        ref AnalysisCollector collector)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg is { Key: "IgnoredHeaders", Value.Values.IsDefaultOrEmpty: false })
            {
                foreach (var value in arg.Value.Values)
                {
                    if (value.Value?.ToString() is { } headerName)
                    {
                        collector.IgnoredHeaders.Add(headerName);
                    }
                }
            }
            else if (arg is
                     {
                         Key: "CreatedTypeProxy",
                         Value: { Kind: TypedConstantKind.Type, Value: INamedTypeSymbol proxySymbol }
                     })
            {
                collector.AddProxy(
                    proxySymbol,
                    attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation());
            }
        }
    }

    public static void ParseAssembly(
        ITypeSymbol targetType,
        IAssemblySymbol assembly,
        ref readonly FlameSymbols symbols,
        CancellationToken cancellationToken,
        ref AnalysisCollector collector)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrSymbol) continue;

            cancellationToken.ThrowIfCancellationRequested();

            if (symbols.IsCsvAssemblyTypeFieldAttribute(attrSymbol))
            {
                if (SymbolEqualityComparer.Default.Equals(
                        targetType,
                        attribute.ConstructorArguments[0].Value as ITypeSymbol))
                {
                    collector.TargetAttributes.Add(new TargetAttributeModel(attribute, isAssemblyAttribute: true));
                }
            }
            else if (symbols.IsCsvAssemblyTypeAttribute(attrSymbol))
            {
                if (SymbolEqualityComparer.Default.Equals(
                        targetType,
                        attribute.ConstructorArguments[0].Value as ITypeSymbol))
                {
                    Parse(attribute, cancellationToken, ref collector);
                }
            }
        }
    }
}
