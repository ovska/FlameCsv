
namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private string GetParserInitializer(
        ITypeSymbol token,
        ITypeSymbol memberType,
        ITypeSymbol parser,
        INamedTypeSymbol converterFactorySymbol)
    {
        bool found = false;

        // TODO: find ctor that accepts CsvOptions<T>
        foreach (var member in parser.GetMembers())
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Constructor, Parameters.IsDefaultOrEmpty: true })
            {
                found = true;
                break;
            }
        }

        if (!found)
            throw new Exception("No empty constructor found for " + parser.ToDisplayString()); // TODO

        bool isFactory = false;

        foreach (var iface in parser.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, converterFactorySymbol))
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

    private bool CreateStaticInstance(INamedTypeSymbol classSymbol)
    {
        bool hasEmptyCtor = false;

        foreach (var ctor in classSymbol.Constructors)
        {
            if (ctor.Parameters.IsDefaultOrEmpty)
            {
                hasEmptyCtor = true;
                break;
            }
        }

        if (!hasEmptyCtor)
            return false;

        foreach (var symbol in classSymbol.GetMembers("Instance"))
        {
            if (symbol.IsStatic &&
                symbol.CanBeReferencedByName &&
                symbol.Kind is SymbolKind.Field or SymbolKind.Property or SymbolKind.Method)
            {
                return false;
            }
        }

        return true;
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
