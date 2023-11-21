using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen;

public readonly struct TypeMapSymbol
{
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

    public TypeMapSymbol(
        INamedTypeSymbol containingClass,
        AttributeSyntax csvTypeMapAttribute,
        INamedTypeSymbol attributeSymbol,
        SourceProductionContext context)
    {
        ContainingClass = containingClass;
        TokenSymbol = attributeSymbol.TypeArguments[0];
        Type = attributeSymbol.TypeArguments[1];
        Context = context;
        Token = TokenSymbol.ToDisplayString();
        ResultName = Type.ToDisplayString();
        HandlerArgs = $"(ref TypeMapMaterializer materializer, ref ParseState state, ReadOnlySpan<{Token}> field)";

        if (csvTypeMapAttribute.ArgumentList is { } arguments)
        {
            foreach (var arg in arguments.Arguments)
            {
                NameEqualsSyntax? propertyNameNode = null;
                SyntaxNode? propertyValueNode = null;

                using (var enumerator = arg.ChildNodes().GetEnumerator())
                {
                    if (enumerator.MoveNext())
                        propertyNameNode = enumerator.Current as NameEqualsSyntax;

                    if (enumerator.MoveNext())
                        propertyValueNode = enumerator.Current;
                }

                if (propertyNameNode is null || propertyValueNode is null)
                    continue;

                string propertyName = propertyNameNode.Name.Identifier.ValueText;

                if (propertyName.Equals("IgnoreUnmatched", StringComparison.OrdinalIgnoreCase))
                {
                    IgnoreUnmatched = propertyValueNode.IsKind(SyntaxKind.TrueLiteralExpression);
                }
                else if (propertyName.Equals("ThrowOnDuplicate", StringComparison.OrdinalIgnoreCase))
                {
                    ThrowOnDuplicate = propertyValueNode.IsKind(SyntaxKind.TrueLiteralExpression);
                }
                else if (propertyName.Equals("SkipStaticInstance", StringComparison.OrdinalIgnoreCase))
                {
                    SkipStaticInstance = propertyValueNode.IsKind(SyntaxKind.TrueLiteralExpression);
                }
            }
        }

        TargetedHeaders = [];

        foreach (var attribute in Type.GetAttributes())
        {
            if (attribute.AttributeClass?.MetadataName == "FlameCsv.Binding.Attributes.CsvHeaderTargetAttribute")
            {
                var member = attribute.ConstructorArguments[0].Value as string;

                if (!TargetedHeaders.TryGetValue(member!, out var set))
                {
                    TargetedHeaders[member!] = set = [];
                }

                foreach (var value in attribute.ConstructorArguments[1].Values)
                {
                    set.Add((string)value.Value!);
                }
            }
        }
    }

    public Dictionary<string, HashSet<string>> TargetedHeaders { get; }

    /// <summary>
    /// Current context.
    /// </summary>
    public SourceProductionContext Context { get; }

    public bool IgnoreUnmatched { get; }

    public bool ThrowOnDuplicate { get; }

    public bool SkipStaticInstance { get; }

    public void ThrowIfCancellationRequested() => Context.CancellationToken.ThrowIfCancellationRequested();

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
        List<string> wrappers = [];

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
