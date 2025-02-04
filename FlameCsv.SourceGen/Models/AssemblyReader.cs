namespace FlameCsv.SourceGen.Models;

internal static class AssemblyReader
{
    public static void Read(
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
                    var model = new TargetAttributeModel(attribute, isAssemblyAttribute: true, cancellationToken);
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();
                    collector.AddTargetAttribute(model, location);
                }
            }
            else if (symbols.IsCsvAssemblyTypeAttribute(attrSymbol))
            {
                if (SymbolEqualityComparer.Default.Equals(
                        targetType,
                        attribute.ConstructorArguments[0].Value as ITypeSymbol))
                {
                    foreach (var args in attribute.NamedArguments)
                    {
                        if (args.Key == "IgnoredHeaders")
                        {
                            foreach (var value in args.Value.Values)
                            {
                                if (value.Value?.ToString() is { Length: > 0 } header)
                                {
                                    collector.AddIgnoredHeader(header);
                                }
                            }
                        }
                        else if (args.Key == "CreatedTypeProxy")
                        {
                            collector.AddProxy(
                                (ITypeSymbol)args.Value.Value!,
                                attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation());
                        }
                    }
                }
            }
        }
    }
}
