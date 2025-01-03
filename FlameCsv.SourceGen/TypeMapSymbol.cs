using FlameCsv.SourceGen.Bindings;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen;

internal readonly partial struct TypeMapSymbol
{
    /// <summary>
    /// Class annotated with the CsvTypeMapAttribute.
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
    /// <c>(ref TypeMapMaterializer, ref ParseState, ReadOnlySpan&lt;Token&gt;)</c>
    /// </summary>
    public string ParseHandlerArgs { get; }

    public BindingScope Scope { get; }

    public bool UseBuiltinConverters { get; }

    public readonly KnownSymbols Symbols;

    private readonly Lazy<TypeBindings> _typeBindings;

    /// <summary>
    /// Whether to skip checking for a valid constructor (only used for writing).
    /// </summary>
    public bool SkipConstructor => Scope == BindingScope.Write;

    public TypeMapSymbol(
        Compilation compilation,
        INamedTypeSymbol containingClass,
        AttributeData attribute,
        SourceProductionContext context)
    {
        Symbols = new KnownSymbols(compilation);
        ContainingClass = containingClass;
        TokenSymbol = attribute.AttributeClass!.TypeArguments[0];
        Type = attribute.AttributeClass!.TypeArguments[1];
        Context = context;
        Token = TokenSymbol.ToDisplayString();
        ResultName = Type.ToDisplayString();
        ParseHandlerArgs = $"(TypeMapMaterializer materializer, ref ParseState state, ReadOnlySpan<{Token}> field)";
        UseBuiltinConverters = true;

        _typeBindings = new(ResolveMembers);

        foreach (var kvp in attribute.NamedArguments)
        {
            string propertyName = kvp.Key;
            object? value = kvp.Value.Value;

            if (propertyName.Equals("IgnoreUnmatched", StringComparison.Ordinal))
            {
                IgnoreUnmatched = value is true;
            }
            else if (propertyName.Equals("ThrowOnDuplicate", StringComparison.Ordinal))
            {
                ThrowOnDuplicate = value is true;
            }
            else if (propertyName.Equals("UseBuiltinConverters", StringComparison.Ordinal))
            {
                UseBuiltinConverters = value is not false;
            }
            else if (propertyName.Equals("Scope", StringComparison.Ordinal))
            {
                Scope = value switch
                {
                    BindingScope bs => bs,
                    _ => throw new NotSupportedException("Unrecognized binding scope: " + kvp.Value.ToCSharpString()),
                };
            }
        }
    }

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

    public void WriteWrappedTypes(StringBuilder sb, out int wrappedCount)
    {
        if (ContainingClass.ContainingType is null)
        {
            wrappedCount = 0;
            return;
        }

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
    }
}
