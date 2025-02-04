namespace FlameCsv.SourceGen.Models;

internal record ConverterModel
{
    /// <summary>
    /// Returns a converter override, or null.
    /// </summary>
    public static ConverterModel? Create(
        ITypeSymbol token,
        ISymbol propertyOrParameter,
        ITypeSymbol convertedType,
        ref readonly FlameSymbols symbols)
    {
        foreach (var attributeData in propertyOrParameter.GetAttributes())
        {
            if (attributeData.AttributeClass is { IsGenericType: true, Arity: 2 } attribute &&
                SymbolEqualityComparer.Default.Equals(token, attribute.TypeArguments[0]) &&
                symbols.IsCsvConverterOfTAttribute(attribute.ConstructUnboundGenericType()))
            {
                return new ConverterModel(token, convertedType, attribute.TypeArguments[1], in symbols);
            }
        }

        return null;
    }

    public TypeRef ConvertedType { get; }
    public TypeRef ConverterType { get; }
    public ConstructorArgumentType ConstructorArguments { get; }
    public bool WrapInNullable { get; }
    public bool IsFactory { get; }

    public ConverterModel(
        ITypeSymbol token,
        ITypeSymbol convertedType,
        ITypeSymbol converter,
        ref readonly FlameSymbols symbols)
    {
        ConvertedType = new TypeRef(convertedType);
        ConverterType = new TypeRef(converter);
        IsFactory = converter.Inherits(symbols.GetCsvConverterFactoryType(token));

        ConstructorArguments = ConstructorArgumentType.Invalid;

        foreach (var member in converter.GetMembers())
        {
            if (member.Kind == SymbolKind.Method &&
                member is IMethodSymbol { MethodKind: MethodKind.Constructor } method)
            {
                if (method.Parameters.IsEmpty)
                {
                    // don't break, we might find a better constructor
                    ConstructorArguments = ConstructorArgumentType.Empty;
                    continue;
                }

                if (method.Parameters.Length == 1)
                {
                    if (SymbolEqualityComparer.Default.Equals(
                            method.Parameters[0].Type,
                            symbols.GetCsvOptionsType(token)))
                    {
                        ConstructorArguments = ConstructorArgumentType.Options;
                        break; // we found the best constructor
                    }
                }
            }
        }

        // wrap in nullable if needed, e.g., property is 'int?' but the converter is for int
        // leave factories as they are since we can't statically analyze if they support nullable types
        if (!IsFactory && convertedType.IsNullable(out var baseType))
        {
            INamedTypeSymbol? current = converter.BaseType;
            ITypeSymbol? resultType = null;

            while (current != null)
            {
                if (current.IsGenericType)
                {
                    if (symbols.IsCsvConverterTTValue(current.ConstructUnboundGenericType()))
                    {
                        resultType = current.TypeArguments[1];
                        break;
                    }
                }

                current = current.BaseType;
            }

            // if resultType is "int" here and convertedType is "int?", we need to wrap it in a NullableConverter
            WrapInNullable = SymbolEqualityComparer.Default.Equals(baseType, resultType);
        }
    }

    /// <summary>
    /// Adds diagnostic for invalid constructor if applicable.
    /// </summary>
    public void TryAddDiagnostics(ISymbol target, ITypeSymbol tokenType, ref List<Diagnostic>? diagnostics)
    {
        if (ConstructorArguments is ConstructorArgumentType.Invalid)
        {
            (diagnostics ??= []).Add(Diagnostics.NoCsvFactoryConstructor(target, ConverterType.Name, tokenType));
        }
        else if (ConverterType.IsAbstract)
        {
            (diagnostics ??= []).Add(Diagnostics.CsvConverterAbstract(target, ConverterType.Name));
        }
    }
}
