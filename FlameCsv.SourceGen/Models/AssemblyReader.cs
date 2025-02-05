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
                    collector.TargetAttributes.Add(new TargetAttributeModel(attribute, isAssemblyAttribute: true));
                }
            }
            else if (symbols.IsCsvAssemblyTypeAttribute(attrSymbol))
            {
                if (SymbolEqualityComparer.Default.Equals(
                        targetType,
                        attribute.ConstructorArguments[0].Value as ITypeSymbol))
                {
                    TypeAttributeModel.Parse(attribute, cancellationToken, ref collector);
                }
            }
        }
    }
}
