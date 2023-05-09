namespace FlameCsv.SourceGen;

internal sealed class LazySymbol : Lazy<INamedTypeSymbol>
{
    public LazySymbol(Compilation compilation, string name, bool unboundGeneric = false)
        : base(
            () =>
            {
                try
                {
                    var symbol = compilation.GetTypeByMetadataName(name)
                        ?? throw new Exception($"Type '{name}' not found from metadata.");

                    if (unboundGeneric)
                    {
                        symbol = symbol.ConstructUnboundGenericType();
                    }

                    return symbol;
                }
                catch (Exception e)
                {
                    throw new Exception("Error, could not find " + name, e);
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication)
    {
    }
}
