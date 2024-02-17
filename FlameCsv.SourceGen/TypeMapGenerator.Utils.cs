
namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void ResolveConverter(
        StringBuilder sb,
        in TypeMapSymbol typeMap,
        ISymbol propertyOrField,
        ITypeSymbol type,
        INamedTypeSymbol converterFactorySymbol)
    {
        foreach (var attributeData in propertyOrField.GetAttributes())
        {
            if (attributeData.AttributeClass is { IsGenericType: true } attribute &&
                SymbolEqualityComparer.Default.Equals(typeMap.TokenSymbol, attribute.TypeArguments[0]) &&
                SymbolEqualityComparer.Default.Equals(attribute.ConstructUnboundGenericType(), _symbols.CsvConverterOfTAttribute))
            {
                ResolveExplicitConverter(
                    in typeMap,
                    sb,
                    type,
                    attribute.TypeArguments[1],
                    converterFactorySymbol);
                return;
            }
        }

        bool isNullable = false;

        if (typeMap.UseBuiltinConverters)
        {
            type = type.UnwrapNullable(out isNullable);
        }

        string typeName = type.ToDisplayString();

        if (isNullable)
        {
            sb.Append("new NullableConverter<");
            sb.Append(typeMap.Token);
            sb.Append(", ");
            sb.Append(typeName);
            sb.Append(">(");
        }

        if (typeMap.UseBuiltinConverters &&
            type.TypeKind == TypeKind.Enum &&
            typeMap.GetEnumConverterOrNull() is string enumConverter)
        {
            sb.Append("new ");
            sb.Append(enumConverter);
            sb.Append('<');
            sb.Append(typeName);
            sb.Append(">(options)");
        }
        else
        {
            sb.Append("options.GetConverter<");
            sb.Append(typeName);
            sb.Append(">()");
        }

        if (isNullable)
        {
            sb.Append(", options.GetNullToken(typeof(");
            sb.Append(typeName);
            sb.Append(")))");
        }
    }

    private void ResolveExplicitConverter(
        in TypeMapSymbol typeMap,
        StringBuilder sb,
        ITypeSymbol memberType,
        ITypeSymbol parser,
        INamedTypeSymbol converterFactorySymbol)
    {
        string? foundArgs = null;
        var csvOptionsSymbol = _symbols.GetCsvOptionsType(typeMap.TokenSymbol);

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
            typeMap.Fail(Diagnostics.NoCsvFactoryConstructorFound(parser));

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
