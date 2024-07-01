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

        INamedTypeSymbol? optionsSymbol = typeMap.Symbols.GetExplicitOptionsType(typeMap.TokenSymbol);
        bool canUseDefault =
            IsDefaultConverterSupported(in typeMap.Symbols, type, out string defaultName) &&
            typeMap.UseBuiltinConverters &&
            optionsSymbol is not null;

        string optionsName = "options";

        if (isNullable)
        {
            if (canUseDefault)
            {
                sb.Append("FlameCsv.Converters.DefaultConverters.GetOrCreate((");
                sb.Append(optionsSymbol!.ToDisplayString());
                sb.Append(")options, static o => ");
                optionsName = "o";
            }

            sb.Append("new FlameCsv.Converters.NullableConverter<");
            sb.Append(typeMap.Token);
            sb.Append(", ");
            sb.Append(typeName);
            sb.Append(">(");
        }

        if (canUseDefault)
        {
            sb.Append("FlameCsv.Converters.DefaultConverters.Create");
            sb.Append(defaultName);
            sb.Append("((");
            sb.Append(optionsSymbol!.ToDisplayString());
            sb.Append(')');
            sb.Append(optionsName);
            sb.Append(')');

            if (typeMap.Symbols.Nullable)
                sb.Append('!');
        }
        else
        {
            sb.Append("options.GetConverter<");
            sb.Append(typeName);
            sb.Append(">()");
        }

        if (isNullable)
        {
            sb.Append(", ");
            sb.Append(optionsName);
            sb.Append(".GetNullToken(typeof(");
            sb.Append(typeName);
            sb.Append(")))");

            if (canUseDefault)
            {
                sb.Append(")");
            }
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
        INamedTypeSymbol? explicitOptions = typeMap.Symbols.GetExplicitOptionsType(typeMap.TokenSymbol);
        bool foundExplicit = false;
        bool foundInstance = false;

        foreach (var member in parser.GetMembers())
        {
            if (member.Kind == SymbolKind.Method &&
                member is IMethodSymbol { MethodKind: MethodKind.Constructor } method)
            {
                if (method.Parameters.Length == 1)
                {
                    if (SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, explicitOptions))
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
        if (string.IsNullOrEmpty(foundArgs) && foundExplicit)
        {
            foundArgs = $"({explicitOptions!.ToDisplayString()})options";
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

    private bool IsDefaultConverterSupported(in KnownSymbols symbols, ITypeSymbol type, out string name)
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

        if ((type.Name == "DateTime" && SymbolEqualityComparer.Default.Equals(type, symbols.SystemDateTime)) ||
            (type.Name == "DateTimeOffset" && SymbolEqualityComparer.Default.Equals(type, symbols.SystemDateTimeOffset)) ||
            (type.Name == "TimeSpan" && SymbolEqualityComparer.Default.Equals(type, symbols.SystemTimeSpan)) ||
            (type.Name == "Guid" && SymbolEqualityComparer.Default.Equals(type, symbols.SystemGuid)))
        {
            name = type.Name;
            return true;
        }

        name = "";
        return false;
    }
}
