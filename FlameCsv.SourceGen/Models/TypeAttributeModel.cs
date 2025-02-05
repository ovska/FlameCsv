namespace FlameCsv.SourceGen.Models;

internal readonly record struct TypeAttributeModel
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
                collector.Proxies.Add(proxySymbol);
                collector.AddProxy(
                    proxySymbol,
                    attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation());
            }
        }
    }
}
