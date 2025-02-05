using System.Collections.Immutable;

namespace FlameCsv.SourceGen.Models;

internal sealed class TargetAttributeModel
{
    public bool IsForAssembly { get; }
    public string MemberName { get; }
    public bool IsParameter { get; }

    public bool? IsIgnored { get; }
    public bool? IsRequired { get; }
    public int? Order { get; }
    public int? Index { get; }

    public ImmutableArray<TypedConstant> Names { get; }

    /// <summary>
    /// Whether a parameter or member with name <see cref="MemberName"/> was found.
    /// </summary>
    public bool MatchFound { get; set; }

    public Location? GetLocation(CancellationToken cancellationToken)
        => (_syntaxNode ??= _syntaxReference?.GetSyntax(cancellationToken))?.GetLocation();

    private readonly SyntaxReference? _syntaxReference;
    private SyntaxNode? _syntaxNode;

    public TargetAttributeModel(AttributeData attribute, bool isAssemblyAttribute)
    {
        // assembly attributes have the target type as the first argument
        int startIndex = isAssemblyAttribute ? 1 : 0;

        IsForAssembly = isAssemblyAttribute;
        MemberName = attribute.ConstructorArguments[startIndex].Value?.ToString() ?? "";
        Names = attribute.ConstructorArguments[startIndex + 1].Values; // defer equatable array creation
        _syntaxReference = attribute.ApplicationSyntaxReference;

        foreach (var kvp in attribute.NamedArguments)
        {
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
                Order = kvp.Value.Value as int?;
            }
            else if (kvp.Key == "Index")
            {
                Index = kvp.Value.Value as int?;
            }
        }
    }

    public override bool Equals(object? obj) => throw new NotSupportedException();
    public override int GetHashCode() => throw new NotSupportedException();
}
