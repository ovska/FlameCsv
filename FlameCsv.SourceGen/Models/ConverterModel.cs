namespace FlameCsv.SourceGen.Models;

internal sealed record ConverterModel
{
    public static ConverterModel? GetOverriddenConverter(
        ITypeSymbol token,
        ISymbol propertyOrParameter,
        ITypeSymbol convertedType,
        FlameSymbols symbols)
    {
        foreach (var attributeData in propertyOrParameter.GetAttributes())
        {
            if (attributeData.AttributeClass is { IsGenericType: true } attribute &&
                SymbolEqualityComparer.Default.Equals(token, attribute.TypeArguments[0]) &&
                SymbolEqualityComparer.Default.Equals(
                    attribute.ConstructUnboundGenericType(),
                    symbols.CsvConverterOfTAttribute))
            {
                return new ConverterModel(token, convertedType, attribute.TypeArguments[1], symbols);
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
        FlameSymbols symbols)
    {
        ConvertedType = new TypeRef(convertedType);
        ConverterType = new TypeRef(converter);
        IsFactory = converter.Inherits(symbols.GetCsvConverterFactoryType(convertedType));

        ConstructorArguments = ConstructorArgumentType.Invalid;
        INamedTypeSymbol csvOptionsSymbol = symbols.GetCsvOptionsType(token);

        foreach (var member in converter.GetMembers())
        {
            if (member.Kind == SymbolKind.Method &&
                member is IMethodSymbol { MethodKind: MethodKind.Constructor } method)
            {
                if (method.Parameters.Length == 1)
                {
                    if (SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, csvOptionsSymbol))
                    {
                        ConstructorArguments = ConstructorArgumentType.Options;
                        break;
                    }
                }
                else if (method.Parameters.IsEmpty)
                {
                    ConstructorArguments = ConstructorArgumentType.None;
                }
            }
        }

        // wrap in a NullableConverter if needed, find base type
        if (convertedType.IsNullable(out var baseType))
        {
            INamedTypeSymbol? current = converter.BaseType;
            ITypeSymbol? resultType = null;

            while (current != null)
            {
                if (current.IsGenericType)
                {
                    INamedTypeSymbol generic = current.ConstructUnboundGenericType();

                    if (SymbolEqualityComparer.Default.Equals(generic, symbols.CsvConverterFactory))
                    {
                        resultType = current.TypeArguments[0];
                        break;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(generic, symbols.CsvConverterTTValue))
                    {
                        resultType = current.TypeArguments[1];
                        break;
                    }
                }

                current = current.BaseType;
            }

            WrapInNullable = SymbolEqualityComparer.Default.Equals(baseType, resultType);
        }
    }
}
