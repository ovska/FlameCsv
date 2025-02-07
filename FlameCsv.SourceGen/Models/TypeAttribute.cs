namespace FlameCsv.SourceGen.Models;

internal static class TypeAttribute
{
    public static void Parse(
        AttributeData attribute,
        ref AnalysisCollector collector)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == "IgnoredHeaders")
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
                         Value: { Kind: TypedConstantKind.Type, Value: ITypeSymbol proxySymbol }
                     })
            {
                collector.AddProxy(proxySymbol, attribute.GetLocation());
            }
        }
    }

    public static void ParseAssembly(
        ITypeSymbol targetType,
        IAssemblySymbol assembly,
        CancellationToken cancellationToken,
        ref ConstructorModel? typeConstructor,
        ref readonly FlameSymbols symbols,
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
                    Parse(attribute, ref collector);
                }
            }
            else if (symbols.IsCsvConstructorAttribute(attrSymbol))
            {
                if (attribute.GetNamedArgument("TargetType") is ITypeSymbol target &&
                    SymbolEqualityComparer.Default.Equals(targetType, target))
                {
                    typeConstructor = ConstructorModel.ParseConstructorAttribute(target, attribute);
                }
            }
        }
    }
}
