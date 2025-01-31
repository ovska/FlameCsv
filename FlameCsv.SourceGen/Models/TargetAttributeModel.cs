using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record TargetAttributeModel
{
    public int? Index { get; }
    public string MemberName { get; }
    public bool IsIgnored { get; }
    public bool IsRequired { get; }
    public int Order { get; }
    public ImmutableEquatableArray<string> Names { get; }

    public TargetAttributeModel(AttributeData attribute)
    {
        MemberName = attribute.ConstructorArguments[0].Value?.ToString() ?? "";

        // params-array
        if (attribute.ConstructorArguments[1].Values is { IsDefaultOrEmpty: false } namesArray)
        {
            Names = [..namesArray.Select(x => x.Value?.ToString()).OfType<string>()];
        }
        else
        {
            Names = ImmutableEquatableArray<string>.Empty;
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
                Order = (int)kvp.Value.Value!;
            }
            else if (kvp.Key == "Index")
            {
                Index = kvp.Value.Value is int index and >= 0 ? index : null;
            }
        }
    }
}
