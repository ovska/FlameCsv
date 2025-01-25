using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen.Models;

internal sealed record TypeMapModel
{
    public bool Invalid { get; set; }

    public TypeMapModel(INamedTypeSymbol containingClass, AttributeData attribute)
    {
        TypeMap = new TypeRef(containingClass);
        Token = new TypeRef(attribute.AttributeClass!.TypeArguments[0]);
        Type = new TypeRef(attribute.AttributeClass.TypeArguments[1]);

        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key.Equals("Scope", StringComparison.Ordinal))
            {
                Scope = kvp.Value.Value switch
                {
                    CsvBindingScope.All => CsvBindingScope.All,
                    CsvBindingScope.Write => CsvBindingScope.Write,
                    CsvBindingScope.Read => CsvBindingScope.Read,
                    _ => throw new NotSupportedException("Unrecognized binding scope: " + kvp.Value.ToCSharpString()),
                };
                // TODO: ReportDiagnostic instead of exception?
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
    public CsvBindingScope Scope { get; }
}
