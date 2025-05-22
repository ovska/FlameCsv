using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct ConverterModel
{
    /// <summary>
    /// Returns a converter override, or null.
    /// </summary>
    public static ConverterModel? Create(
        ISymbol propertyOrParameter,
        ITypeSymbol convertedType,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector
    )
    {
        if (!TryGetConverterAttribute(propertyOrParameter, in symbols, out var converter))
        {
            return null;
        }

        TypeRef converterType = new(converter);
        ConstructorArgumentType constructorArguments = default;
        bool wrapInNullable = false;
        bool isFactory = IsConverterFactory(converter, in symbols);

        foreach (var member in converter.GetMembers())
        {
            if (
                member.Kind == SymbolKind.Method
                && member is IMethodSymbol { MethodKind: MethodKind.Constructor } method
            )
            {
                if (method.Parameters.IsEmpty)
                {
                    // don't break, we might find a better constructor
                    constructorArguments = ConstructorArgumentType.Empty;
                    continue;
                }

                if (method.Parameters.Length == 1)
                {
                    if (symbols.IsCsvOptionsOfT(method.Parameters[0].Type))
                    {
                        constructorArguments = ConstructorArgumentType.Options;
                        break; // we found the best constructor
                    }
                }
            }
        }

        // wrap in nullable if needed, e.g., property is 'int?' but the converter is for int
        // leave factories as they are since we can't statically analyze if they support nullable types
        if (!isFactory && convertedType.IsNullable(out var baseType))
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
            wrapInNullable = SymbolEqualityComparer.Default.Equals(baseType, resultType);
        }

        if (constructorArguments is ConstructorArgumentType.Invalid)
        {
            collector.AddDiagnostic(Diagnostics.NoCsvFactoryConstructor(converter, converterType.Name, convertedType));
        }
        else if (converterType.IsAbstract)
        {
            collector.AddDiagnostic(Diagnostics.CsvConverterAbstract(converter, converterType.Name));
        }

        return new ConverterModel
        {
            ConverterType = converterType,
            ConstructorArguments = constructorArguments,
            WrapInNullable = wrapInNullable,
            IsFactory = isFactory,
        };
    }

    public required TypeRef ConverterType { get; init; }
    public required ConstructorArgumentType ConstructorArguments { get; init; }
    public required bool WrapInNullable { get; init; }
    public required bool IsFactory { get; init; }

    public static bool IsConverterFactory(ITypeSymbol? type, ref readonly FlameSymbols symbols)
    {
        while (type is not null)
        {
            if (symbols.IsCsvConverterFactoryOfT(type))
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }

    public static bool IsConverterOrFactory(ITypeSymbol? type, ITypeSymbol tokenType, ref readonly FlameSymbols symbols)
    {
        while (type is not null)
        {
            if (type is INamedTypeSymbol genericType)
            {
                if (genericType.Arity == 2)
                {
                    if (
                        symbols.IsCsvConverterTTValue(genericType.ConstructUnboundGenericType())
                        && SymbolEqualityComparer.Default.Equals(genericType.TypeArguments[0], tokenType)
                    )
                    {
                        return true;
                    }
                }
                else if (genericType.Arity == 1)
                {
                    if (symbols.IsCsvConverterFactoryOfT(genericType))
                    {
                        return true;
                    }
                }
            }

            type = type.BaseType;
        }

        return false;
    }

    private static bool TryGetConverterAttribute(
        ISymbol propertyOrParameter,
        ref readonly FlameSymbols symbols,
        [NotNullWhen(true)] out ITypeSymbol? converter
    )
    {
        foreach (var attributeData in propertyOrParameter.GetAttributes())
        {
            if (
                attributeData.AttributeClass is { IsGenericType: true, Arity: 1 } attribute
                && symbols.IsCsvConverterOfTAttribute(attribute.ConstructUnboundGenericType())
                && IsConverterOrFactory(attribute.TypeArguments[0], symbols.TokenType, in symbols)
            )
            {
                converter = attribute.TypeArguments[0];
                return true;
            }
        }

        converter = null;
        return false;
    }
}
