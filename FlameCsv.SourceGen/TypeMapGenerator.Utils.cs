namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static bool IsValidMember(ISymbol member)
    {
        return !member.IsStatic && member.CanBeReferencedByName;
    }

    private bool IsValidProperty(IPropertySymbol p)
    {
        if (!IsValidMember(p) || p.IsReadOnly || p.IsIndexer)
        {
            return false;
        }

        return !HasIgnoreAttribute(p);
    }

    private bool IsValidField(IFieldSymbol f)
    {
        if (!IsValidMember(f) || f.IsReadOnly)
            return false;

        return !HasIgnoreAttribute(f);
    }

    private bool HasIgnoreAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _symbols.CsvHeaderIgnoreAttribute))
                return true;
        }

        return false;
    }

    private static string Stringify(string? name)
    {
        if (name is null)
            return "null";

        return $"\"{name.Replace("\"", "\\\"")}\"";
    }

    private string GetParserInitializer(ITypeSymbol token, ITypeSymbol memberType, ITypeSymbol parser)
    {
        if (!parser.GetMembers().Any(m => m is IMethodSymbol { MethodKind: MethodKind.Constructor, Parameters.Length: 0 }))
            throw new Exception("No empty constructor found for " + parser.ToDisplayString()); // TODO

        var parserFactory = _symbols.CsvConverterFactory.ConstructedFrom.Construct(token);
        bool isFactory = false;

        foreach (var iface in parser.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, parserFactory))
            {
                isFactory = true;
                break;
            }
        }

        var init = $"new {parser.ToDisplayString()}()";

        if (!isFactory)
            return init;

        // Cast in case its explicitly implemented
        return $"((CsvConverterFactory<{token.ToDisplayString()}>){init}).Create<{memberType.ToDisplayString()}>(options)";
    }

    private bool IsReferenceOrContainsReferences(ITypeSymbol symbol)
    {
        if (symbol.IsUnmanagedType)
            return false;

        if (symbol.IsReferenceType)
            return true;

        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol field && !field.IsStatic && IsReferenceOrContainsReferences(field.Type))
                return true;

            if (member is IPropertySymbol property && !property.IsStatic && IsReferenceOrContainsReferences(property.Type))
                return true;
        }

        return false;
    }

    private string GetAccessModifier(INamedTypeSymbol classSymbol)
    {
        return classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "protected internal",
            Accessibility.ProtectedOrInternal => "private protected",
            Accessibility.Private => "private",
            _ => throw new NotSupportedException(),
        };
    }
}
