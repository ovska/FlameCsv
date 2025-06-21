using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct NestedType
{
    public string Name { get; }
    public bool IsReadOnly => (_config & Config.IsReadOnly) != 0;
    public bool IsRefLikeType => (_config & Config.IsRefLikeType) != 0;
    public bool IsAbstract => (_config & Config.IsAbstract) != 0;
    public bool IsValueType => (_config & Config.IsValueType) != 0;

    private readonly Config _config;

    private NestedType(INamedTypeSymbol type)
    {
        Name = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        _config =
            (type.IsReadOnly ? Config.IsReadOnly : 0)
            | (type.IsRefLikeType ? Config.IsRefLikeType : 0)
            | (type.IsAbstract ? Config.IsAbstract : 0)
            | (type.IsValueType ? Config.IsValueType : 0);
    }

    public static EquatableArray<NestedType> Parse(
        ITypeSymbol containingClass,
        CancellationToken cancellationToken,
        List<Diagnostic> diagnostics
    )
    {
        INamedTypeSymbol? type = containingClass.ContainingType;

        if (type is null)
            return [];

        List<NestedType> wrappers = PooledList<NestedType>.Acquire();

        while (type is not null)
        {
            Diagnostics.EnsurePartial(type, cancellationToken, diagnostics, generationTarget: containingClass);

            wrappers.Add(new NestedType(type));

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

    [Flags]
    private enum Config : byte
    {
        IsReadOnly = 1 << 0,
        IsRefLikeType = 1 << 1,
        IsAbstract = 1 << 2,
        IsValueType = 1 << 3,
    }
}
