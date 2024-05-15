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
            _symbols.GetExplicitOptionsType(typeMap.TokenSymbol) is { } optionsSymbol &&
            IsDefaultConverterSupported(type, out string defaultName))
        {
            sb.Append("FlameCsv.Converters.DefaultConverters.Create");
            sb.Append(defaultName);
            sb.Append("((");
            sb.Append(optionsSymbol.ToDisplayString());
            sb.Append(")options)");
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
        var explicitOptionsSymbol = _symbols.GetExplicitOptionsType(typeMap.TokenSymbol);
        bool foundExplicit = false;
        bool foundInstance = false;

        foreach (var member in parser.GetMembers())
        {
            if (member.Kind == SymbolKind.Method &&
                member is IMethodSymbol { MethodKind: MethodKind.Constructor } method)
            {
                if (method.Parameters.Length == 1)
                {
                    if (SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, explicitOptionsSymbol))
                    {
                        foundExplicit = true;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, csvOptionsSymbol))
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

        // if no CsvOptions<T> constructor found, use CsvTextOptions or CsvUtf8Options with cast
        if (string.IsNullOrEmpty(foundArgs) && foundExplicit && explicitOptionsSymbol is not null)
        {
            foundArgs = $"({explicitOptionsSymbol.ToDisplayString()})options";
        }

        if (foundArgs is null && !foundInstance)
            typeMap.Fail(Diagnostics.NoCsvFactoryConstructorFound(parser));

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
    }

    private bool IsDefaultConverterSupported(ITypeSymbol type, out string name)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Char:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
                name = type.Name;
                return true;
        }

        if (type.BaseType is { SpecialType: SpecialType.System_Enum })
        {
            name = $"<{type.ToDisplayString()}>";
            return true;
        }


        if (SymbolEqualityComparer.Default.Equals(type, _symbols.DateTime) ||
            SymbolEqualityComparer.Default.Equals(type, _symbols.DateTimeOffset) ||
            SymbolEqualityComparer.Default.Equals(type, _symbols.TimeSpan) ||
            SymbolEqualityComparer.Default.Equals(type, _symbols.Guid))
        {
            name = type.Name;
            return true;
        }

        name = "";
        return false;
    }
}
