using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record TargetAttributeModel
{
    public string MemberName { get; }
    public bool IsRequired { get; }
    public CsvBindingScope Scope { get; }
    public int Order { get; }
    public ImmutableUnsortedArray<string> Names { get; }

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
            Names = ImmutableUnsortedArray<string>.Empty;
        }

        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == "Scope")
            {
                Scope = (CsvBindingScope)kvp.Value.Value!;
            }

            if (kvp.Key == "IsRequired")
            {
                IsRequired = kvp.Value.Value is true;
            }

            if (kvp.Key == "Order")
            {
                Order = (int)kvp.Value.Value!;
            }
        }
    }
}
