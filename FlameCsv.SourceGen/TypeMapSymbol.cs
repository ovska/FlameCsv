namespace FlameCsv.SourceGen;

public readonly struct TypeMapSymbol
{
    public TypeMapSymbol(
        INamedTypeSymbol containingClass,
        AttributeData csvTypeMapAttribute,
        GeneratorExecutionContext context)
    {
        ContainingClass = containingClass;
        TokenSymbol = csvTypeMapAttribute.AttributeClass!.TypeArguments[0];
        Type = csvTypeMapAttribute.AttributeClass.TypeArguments[1];
        Context = context;
        Token = TokenSymbol.ToDisplayString();
        ResultName = Type.ToDisplayString();
        HandlerArgs = $"(ref TypeMapMaterializer materializer, ref ParseState state, ReadOnlySpan<{Token}> field)";

        foreach (var arg in csvTypeMapAttribute.NamedArguments)
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

        TargetedHeaders = new();

        foreach (var attribute in Type.GetAttributes())
        {
            if (attribute.AttributeClass?.MetadataName == "FlameCsv.Binding.Attributes.CsvHeaderTargetAttribute")
            {
                var member = attribute.ConstructorArguments[0].Value as string;

                if (!TargetedHeaders.TryGetValue(member!, out var set))
                {
                    TargetedHeaders[member!] = set = new HashSet<string>();
                }

                foreach (var value in attribute.ConstructorArguments[1].Values)
                {
                    set.Add((string)value.Value!);
                }
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
    public ITypeSymbol TokenSymbol { get; }

    public string Token { get; }

    public string ResultName { get; }

    public string HandlerArgs { get; }

    public Dictionary<string, HashSet<string>> TargetedHeaders { get; }

    /// <summary>
    /// Current context.
    /// </summary>
    public GeneratorExecutionContext Context { get; }

    public bool IgnoreUnmatched { get; }

    public bool ThrowOnDuplicate { get; }

    public bool SkipStaticInstance { get; }

    /// <exception cref="DiagnosticException" />
    [DoesNotReturn]
    public void Fail(Diagnostic diagnostic)
    {
        Context.ReportDiagnostic(diagnostic);
        throw new DiagnosticException($"Source generation failed: {diagnostic}");
    }

    public string GetWrappedTypes(out int wrappedCount)
    {
        if (ContainingClass.ContainingType is null)
        {
            wrappedCount = 0;
            return "";
        }

        var sb = new StringBuilder();
        List<string> wrappers = new();

        INamedTypeSymbol? type = ContainingClass.ContainingType;

        while (type is not null)
        {
            sb.Append("\n    partial ");
            sb.Append(type.IsValueType ? "struct " : "class ");
            sb.Append(type.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly)));
            sb.Append("  {");
            wrappers.Add(sb.ToString());
            sb.Clear();

            type = type.ContainingType;
        }

        wrappedCount = wrappers.Count;

        wrappers.Reverse();

        foreach (var wrapper in wrappers)
        {
            sb.Append(wrapper);
        }

        return sb.ToString();
    }
}
