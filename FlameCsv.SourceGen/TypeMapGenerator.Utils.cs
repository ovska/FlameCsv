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

    private static bool HasCreateInstanceMethod(TypeMapSymbol typeMap)
    {
        foreach (var member in typeMap.ContainingClass.GetMembers("CreateInstance"))
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, CanBeReferencedByName: true } method)
            {
                if (method.Parameters.Length != 0)
                    throw new Exception("Not parameterless"); // TODO

                if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, typeMap.Type))
                    throw new Exception("Invalid type");

                return true;
            }
        }

        return false;
    }

    private string GetParserInitializer(ITypeSymbol token, ITypeSymbol memberType, ITypeSymbol parser)
    {
        if (!parser.GetMembers().Any(m => m is IMethodSymbol { MethodKind: MethodKind.Constructor, Parameters.Length: 0 }))
            throw new Exception("No empty constructor found for " + parser.ToDisplayString()); // TODO

        var parserFactory = _symbols.ICsvParserFactory.ConstructedFrom.Construct(token);
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
        return $"((ICsvParserFactory<{token.ToDisplayString()}>){init}).Create<{memberType.ToDisplayString()}>(options)";
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
