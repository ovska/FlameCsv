using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen;

internal readonly struct TypeMapSymbol
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

    /// <summary>
    /// Token type name, e.g. <c>char</c>
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Name of the parsed type.
    /// </summary>
    public string ResultName { get; }

    /// <summary>
    /// <c>(ref TypeMapMaterializer, ParseState, ReadOnlySpan&lt;Token&gt;)</c>
    /// </summary>
    public string HandlerArgs { get; }

    public BindingScope Scope { get; }

    /// <summary>
    /// Whether to skip checking for a valid constructor (only used for writing).
    /// </summary>
    public bool SkipConstructor => Scope == BindingScope.Write;

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

                if (propertyName.Equals("IgnoreUnmatched", StringComparison.Ordinal))
                {
                    IgnoreUnmatched = propertyValueNode.IsKind(SyntaxKind.TrueLiteralExpression);
                }
                else if (propertyName.Equals("ThrowOnDuplicate", StringComparison.Ordinal))
                {
                    ThrowOnDuplicate = propertyValueNode.IsKind(SyntaxKind.TrueLiteralExpression);
                }
                else if (propertyName.Equals("Scope", StringComparison.Ordinal))
                {
                    if (propertyValueNode.IsKind(SyntaxKind.DefaultLiteralExpression))
                    {
                        Scope = default;
                    }
                    else if (propertyValueNode is MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "CsvBindingScope" }
                    } maes)
                    {
                        Scope = maes.Name.Identifier.ValueText.AsSpan().Trim() switch
                        {
                            "All" => BindingScope.All,
                            "Read" => BindingScope.Read,
                            "Write" => BindingScope.Write,
                            _ => throw new NotSupportedException(
                                    "Unrecognized binding scope: " + propertyValueNode.ToFullString()),
                        };
                    }
                    else
                    {
                        throw new NotSupportedException(
                            $"Unsupported assignment to \"Scope\": {propertyValueNode.ToFullString()}");
                    }
                }
            }
        }

        TargetedHeaders = [];

        foreach (var attribute in Type.GetAttributes())
        {
            if (attribute.AttributeClass?.MetadataName == "FlameCsv.Binding.Attributes.CsvHeaderTargetAttribute"
                && attribute.ConstructorArguments[0].Value is string member)
            {
                if (!TargetedHeaders.TryGetValue(member, out var set))
                {
                    TargetedHeaders[member] = set = [];
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
            sb.Append("\n    ");

            if (type.IsReadOnly)
                sb.Append("readonly ");
            if (type.IsRefLikeType)
                sb.Append("ref ");
            if (type.IsAbstract)
                sb.Append("abstract ");

            sb.Append("partial ");
            sb.Append(type.IsValueType ? "struct " : "class ");
            sb.Append(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            sb.Append(" {");
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
