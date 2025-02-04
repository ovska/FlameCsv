using FlameCsv.SourceGen.Helpers;

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
                    if (value.Value?.ToString() is { Length: > 0 } headerName)
                    {
                        collector.AddIgnoredHeader(headerName);
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
}

internal readonly record struct TargetAttributeModel
{
    public bool IsForAssembly { get; }
    public int? Index { get; }
    public string MemberName { get; }
    public bool IsParameter { get; }
    public bool IsIgnored { get; }
    public bool IsRequired { get; }
    public int Order { get; }
    public EquatableArray<string> Names { get; }

    public TargetAttributeModel(
        AttributeData attribute,
        bool isAssemblyAttribute,
        CancellationToken cancellationToken)
    {
        int startIndex = isAssemblyAttribute ? 1 : 0;

        IsForAssembly = isAssemblyAttribute;
        MemberName = attribute.ConstructorArguments[startIndex].Value?.ToString() ?? "";
        Names = attribute.ConstructorArguments[startIndex + 1].Values.TryToEquatableStringArray();

        foreach (var kvp in attribute.NamedArguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (kvp.Key == "IsParameter")
            {
                IsParameter = kvp.Value.Value is true;
            }
            else if (kvp.Key == "IsIgnored")
            {
                IsIgnored = kvp.Value.Value is true;
            }
            else if (kvp.Key == "IsRequired")
            {
                IsRequired = kvp.Value.Value is true;
            }
            else if (kvp.Key == "Order")
            {
                Order = kvp.Value.Value as int? ?? 0;
            }
            else if (kvp.Key == "Index")
            {
                Index = kvp.Value.Value is int index and >= 0 ? index : null;
            }
        }
    }
}
