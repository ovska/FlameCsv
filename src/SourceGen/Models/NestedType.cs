using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct NestedType
{
    public required bool IsReadOnly { get; init; }
    public required bool IsRefLikeType { get; init; }
    public required bool IsAbstract { get; init; }
    public required bool IsValueType { get; init; }
    public required string Name { get; init; }

    public static EquatableArray<NestedType> Parse(
        ITypeSymbol containingClass,
        CancellationToken cancellationToken,
        List<Diagnostic> diagnostics)
    {
        INamedTypeSymbol? type = containingClass.ContainingType;

        if (type is null) return [];

        List<NestedType> wrappers = PooledList<NestedType>.Acquire();

        while (type is not null)
        {
            Diagnostics.EnsurePartial(type, cancellationToken, diagnostics, generationTarget: containingClass);

            wrappers.Add(
                new NestedType
                {
                    IsReadOnly = type.IsReadOnly,
                    IsRefLikeType = type.IsRefLikeType,
                    IsAbstract = type.IsAbstract,
                    IsValueType = type.IsValueType,
                    Name = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                });

            type = type.ContainingType;
        }

        wrappers.Reverse();
        return wrappers.ToEquatableArrayAndFree();
    }

    public void WriteTo(IndentedTextWriter writer)
    {
        writer.WriteIf(IsReadOnly, "readonly ");
        writer.WriteIf(IsRefLikeType, "ref ");
        writer.WriteIf(IsAbstract, "abstract ");
        writer.Write("partial ");
        writer.Write(IsValueType ? "struct " : "class ");
        writer.Write(Name);
        writer.WriteLine(" {");
    }
}
