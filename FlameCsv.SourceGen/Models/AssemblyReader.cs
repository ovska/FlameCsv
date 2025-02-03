namespace FlameCsv.SourceGen.Models;

internal static class AssemblyReader
{
    public static void Read(
        ITypeSymbol targetType,
        IAssemblySymbol assembly,
        ref readonly FlameSymbols symbols,
        CancellationToken cancellationToken,
        ref List<(TargetAttributeModel model, Location? location)>? targetAttributeModels,
        ref HashSet<string>? ignoredHeaders,
        ref List<ProxyData>? proxies)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrSymbol) continue;

            cancellationToken.ThrowIfCancellationRequested();

            if (SymbolEqualityComparer.Default.Equals(symbols.CsvAssemblyTypeFieldAttribute, attrSymbol))
            {
                if (SymbolEqualityComparer.Default.Equals(
                        targetType,
                        attribute.ConstructorArguments[0].Value as ITypeSymbol))
                {
                    var model = new TargetAttributeModel(attribute, isAssemblyAttribute: true, cancellationToken);
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();
                    (targetAttributeModels ??= []).Add((model, location));
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(symbols.CsvAssemblyTypeAttribute, attrSymbol))
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
                                    (ignoredHeaders ??= []).Add(header);
                                }
                            }
                        }
                        else if (args.Key == "CreatedTypeProxy")
                        {
                            (proxies ??= []).Add(
                                new ProxyData(
                                    new TypeRef((ITypeSymbol)args.Value.Value!),
                                    attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()));
                        }
                    }
                }
            }
        }
    }
}
