using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen.Models;

internal sealed record TypeMapModel
{
    public bool Invalid { get; set; }

    public TypeMapModel(INamedTypeSymbol containingClass, AttributeData attribute)
    {
        TypeMap = new TypeRef(containingClass);
        Token = new TypeRef(attribute.AttributeClass!.TypeArguments[0]);
        Type = new TypeRef(attribute.AttributeClass!.TypeArguments[1]);

        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key.Equals("Scope", StringComparison.Ordinal))
            {
                Scope = kvp.Value.Value switch
                {
                    BindingScope.All => BindingScope.All,
                    BindingScope.Write => BindingScope.Write,
                    BindingScope.Read => BindingScope.Read,
                    _ => throw new NotSupportedException("Unrecognized binding scope: " + kvp.Value.ToCSharpString()),
                };

                break;
            }
        }


    }

    /// <summary>
    /// TypeRef to the TypeMap object
    /// </summary>
    public TypeRef TypeMap { get; }

    /// <summary>
    /// Ref to the token type
    /// </summary>
    public TypeRef Token { get; }

    /// <summary>
    /// Ref to the converted type.
    /// </summary>
    public TypeRef Type { get; }

    /// <summary>
    /// Scope of the TypeMap.
    /// </summary>
    public BindingScope Scope { get; }
}
