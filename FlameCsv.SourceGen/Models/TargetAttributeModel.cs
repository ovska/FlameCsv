using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct TypeAttributeModel
{
    public static void Parse(
        AttributeData attribute,
        ref List<string>? ignoredHeaders,
        ref List<ProxyData>? proxies)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg is { Key: "IgnoredHeaders", Value.Values.IsDefaultOrEmpty: false })
            {
                foreach (var value in arg.Value.Values)
                {
                    if (value.Value?.ToString() is { Length: > 0 } headerName)
                    {
                        (ignoredHeaders ??= []).Add(headerName);
                    }
                }
            }
            else if (arg is
                     {
                         Key: "CreatedTypeProxy",
                         Value: { Kind: TypedConstantKind.Type, Value: INamedTypeSymbol proxySymbol }
                     })
            {
                (proxies ??= []).Add(
                    new ProxyData(
                        new TypeRef(proxySymbol),
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
            }
        }
    }
}

internal readonly record struct TargetAttributeModel
{
    public int? Index { get; }
    public string MemberName { get; }
    public bool IsIgnored { get; }
    public bool IsRequired { get; }
    public int Order { get; }
    public EquatableArray<string> Names { get; }

    public TargetAttributeModel(AttributeData attribute, bool isAssemblyAttribute)
    {
        int startIndex = isAssemblyAttribute ? 1 : 0;

        MemberName = attribute.ConstructorArguments[startIndex].Value?.ToString() ?? "";

        // params-array
        if (attribute.ConstructorArguments[startIndex + 1].Values is { IsDefaultOrEmpty: false } namesArray)
        {
            Names = [..namesArray.Select(x => x.Value?.ToString()).OfType<string>()];
        }
        else
        {
            Names = EquatableArray<string>.Empty;
        }

        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == "IsIgnored")
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
