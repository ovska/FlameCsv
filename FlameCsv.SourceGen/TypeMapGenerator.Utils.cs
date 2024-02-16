
namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void GetParserInitializer(
        StringBuilder sb,
        ITypeSymbol token,
        ITypeSymbol memberType,
        ITypeSymbol parser,
        INamedTypeSymbol converterFactorySymbol)
    {
        string? foundArgs = null;
        var csvOptionsSymbol = _symbols.GetCsvOptionsType(token);

        foreach (var member in parser.GetMembers())
        {
            if (member.Kind == SymbolKind.Method &&
                member is IMethodSymbol { MethodKind: MethodKind.Constructor } method)
            {
                if (method.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, csvOptionsSymbol))
                {
                    foundArgs = "options";
                    break;
                }
                else if (method.Parameters.IsEmpty)
                {
                    foundArgs = "";
                }
            }
        }

        if (foundArgs is null)
            throw new Exception("No empty constructor found for " + parser.ToDisplayString()); // TODO

        sb.Append("new ");
        sb.Append(parser.ToDisplayString());
        sb.Append('(');
        sb.Append(foundArgs);
        sb.Append(')');

        if (parser.Inherits(converterFactorySymbol))
        {
            sb.Append(".Create<");
            sb.Append(memberType.ToDisplayString());
            sb.Append(">(options)");
        }
    }

    private bool CreateStaticInstance(INamedTypeSymbol classSymbol)
    {
        // check if there is no "Instance" member, and a parameterless exists ctor.
        foreach (var ctor in classSymbol.InstanceConstructors)
        {
            if (ctor.Parameters.IsDefaultOrEmpty)
            {
                return !classSymbol.MemberNames.Contains("Instance");
            }
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
