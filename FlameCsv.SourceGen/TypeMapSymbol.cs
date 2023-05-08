namespace FlameCsv.SourceGen;

public readonly struct TypeMapSymbol
{
    public TypeMapSymbol(
        INamedTypeSymbol containingClass,
        AttributeData attribute,
        GeneratorExecutionContext context)
    {
        ContainingClass = containingClass;
        Token = attribute.AttributeClass!.TypeArguments[0];
        Type = attribute.AttributeClass.TypeArguments[1];
        Context = context;
        TokenName = Token.ToDisplayString();
        ResultName = Type.ToDisplayString();
        HandlerArgs = $"(ref TypeMapState state, ref {ResultName} value, ReadOnlySpan<{TokenName}> field)";

        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key.Equals("IgnoreUnmatched", StringComparison.OrdinalIgnoreCase))
            {
                IgnoreUnmatched = (bool)arg.Value.Value!;
            }
            else if (arg.Key.Equals("ThrowOnDuplicate", StringComparison.OrdinalIgnoreCase))
            {
                ThrowOnDuplicate = (bool)arg.Value.Value!;
            }
            else if (arg.Key.Equals("SkipStaticInstance", StringComparison.OrdinalIgnoreCase))
            {
                SkipStaticInstance = (bool)arg.Value.Value!;
            }
        }
    }

    /// <summary>
    /// Class annotated with the CsvTypeMapAttribute<>
    /// </summary>
    public INamedTypeSymbol ContainingClass { get; }

    /// <summary>
    /// Parsed type of the type map.
    /// </summary>
    public ITypeSymbol Type { get; }

    /// <summary>
    /// Parsed token type.
    /// </summary>
    public ITypeSymbol Token { get; }

    public string TokenName { get; }

    public string ResultName { get; }

    public string HandlerArgs { get; }

    /// <summary>
    /// Current context.
    /// </summary>
    public GeneratorExecutionContext Context { get; }

    public bool IgnoreUnmatched { get; }

    public bool ThrowOnDuplicate { get; }

    public bool SkipStaticInstance { get; }
}
