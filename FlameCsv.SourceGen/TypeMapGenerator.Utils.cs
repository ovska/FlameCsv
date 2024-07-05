namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private void ResolveConverter(
        StringBuilder sb,
        ref readonly TypeMapSymbol typeMap,
        ISymbol propertyOrField,
        ITypeSymbol type,
        INamedTypeSymbol converterFactorySymbol)
    {
        foreach (var attributeData in propertyOrField.GetAttributes())
        {
            if (attributeData.AttributeClass is { IsGenericType: true } attribute &&
                SymbolEqualityComparer.Default.Equals(typeMap.TokenSymbol, attribute.TypeArguments[0]) &&
                SymbolEqualityComparer.Default.Equals(attribute.ConstructUnboundGenericType(), typeMap.Symbols.CsvConverterOfTAttribute))
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

        string optionsName = "options";

        if (isNullable)
        {
            sb.Append("new FlameCsv.Converters.NullableConverter<");
            sb.Append(typeMap.Token);
            sb.Append(", ");
            sb.Append(typeName);
            sb.Append(">(");
        }

        //if (propertyOrField is IPropertySymbol { Name: "DOF" })
        //{
        //    throw new Exception($"{typeMap.TokenSymbol.OriginalDefinition.SpecialType}");
        //}

        if (type.IsEnumOrNullableEnum() &&
            typeMap.TokenSymbol.OriginalDefinition.SpecialType is SpecialType.System_Char or SpecialType.System_Byte)
        {
            sb.Append("options.UseDefaultConverters ? options.GetOrCreate(static o => new FlameCsv.Converters.");

            if (typeMap.TokenSymbol.OriginalDefinition.SpecialType == SpecialType.System_Char)
            {
                sb.Append("EnumTextConverter<");
            }
            else
            {
                sb.Append("EnumUtf8Converter<");
            }

            sb.Append(typeName);
            sb.Append(">(o)) : ");
        }

        sb.Append("options.GetConverter<");
        sb.Append(typeName);
        sb.Append(">()");

        if (isNullable)
        {
            sb.Append(", ");
            sb.Append(optionsName);
            sb.Append(".GetNullToken(typeof(");
            sb.Append(typeName);
            sb.Append(")))");
        }
    }

    private void ResolveExplicitConverter(
        ref readonly TypeMapSymbol typeMap,
        StringBuilder sb,
        ITypeSymbol memberType,
        ITypeSymbol parser,
        INamedTypeSymbol converterFactorySymbol)
    {
        string? foundArgs = null;
        INamedTypeSymbol csvOptionsSymbol = typeMap.Symbols.GetCsvOptionsType(typeMap.TokenSymbol);
        bool foundInstance = false;

        foreach (var member in parser.GetMembers())
        {
            if (member.Kind == SymbolKind.Method &&
                member is IMethodSymbol { MethodKind: MethodKind.Constructor } method)
            {
                if (method.Parameters.Length == 1)
                {
                    if (SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, csvOptionsSymbol))
                    {
                        foundArgs = "options";
                        break;
                    }
                }
                else if (method.Parameters.IsEmpty)
                {
                    foundArgs = "";
                }
            }
            else if (member.IsStatic
                && member.Name == "Instance"
                && member.CanBeReferencedByName
                && member.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                && member.Kind is SymbolKind.Field or SymbolKind.Property)
            {
                foundInstance |= true;
            }
        }

        if (foundArgs is null && !foundInstance)
            typeMap.Fail(Diagnostics.NoCsvFactoryConstructorFound(parser));

        ITypeSymbol baseType = memberType.UnwrapNullable(out bool nullable);
        bool wrapNullable = false;

        if (nullable)
        {
            // wrap in a NullableConverter if needed, find base type
            INamedTypeSymbol? current = parser.BaseType;
            ITypeSymbol? resultType = null;

            while (current != null)
            {
                if (current.IsGenericType)
                {
                    INamedTypeSymbol generic = current.ConstructUnboundGenericType();

                    if (SymbolEqualityComparer.Default.Equals(generic, typeMap.Symbols.CsvConverterFactory))
                    {
                        resultType = current.TypeArguments[0];
                        break;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(generic, typeMap.Symbols.CsvConverterTTValue))
                    {
                        resultType = current.TypeArguments[1];
                        break;
                    }
                }

                current = current.BaseType;
            }

            wrapNullable = SymbolEqualityComparer.Default.Equals(baseType, resultType);
        }

        if (wrapNullable)
        {
            sb.Append("new FlameCsv.Converters.NullableConverter<");
            sb.Append(typeMap.Token);
            sb.Append(", ");
            sb.Append(baseType);
            sb.Append(">(");
        }

        if (string.IsNullOrEmpty(foundArgs) && foundInstance)
        {
            sb.Append(parser.ToDisplayString());
            sb.Append(".Instance");
        }
        else
        {
            sb.Append("new ");
            sb.Append(parser.ToDisplayString());
            sb.Append('(');
            sb.Append(foundArgs);
            sb.Append(')');
        }

        if (parser.Inherits(converterFactorySymbol))
        {
            sb.Append(".Create<");
            sb.Append(memberType.ToDisplayString());
            sb.Append(">(options)");
        }

        if (wrapNullable)
        {
            sb.Append(')');
        }

        if (typeMap.Symbols.Nullable)
            sb.Append('!');
    }
}
