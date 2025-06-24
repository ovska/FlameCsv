using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal interface IConverterModel : IEquatable<IConverterModel>;

internal sealed class StringPoolingConverterModel : IConverterModel
{
    public static readonly StringPoolingConverterModel Instance = new();

    private StringPoolingConverterModel() { }

    public bool Equals(IConverterModel? other) => other is StringPoolingConverterModel;

    public static string GetName(string token, bool full = true)
    {
        if (full)
        {
            return token == "byte"
                ? "global::FlameCsv.Converters.CsvPoolingStringUtf8Converter"
                : "global::FlameCsv.Converters.CsvPoolingStringTextConverter";
        }

        return token == "byte" ? "CsvPoolingStringUtf8Converter" : "CsvPoolingStringTextConverter";
    }
}

internal sealed record ConverterModel : IConverterModel
{
    /// <summary>
    /// Type of the converter override.
    /// </summary>
    public required TypeRef ConverterType { get; init; }

    /// <summary>
    /// Whether the constructor arguments are empty, options, or invalid.
    /// </summary>
    public required ConstructorArgumentType ConstructorArguments { get; init; }

    /// <summary>
    /// Whether the converter should be wrapped in a NullableConverter,
    /// e.g., property is 'int?' but the converter is for int.
    /// This is not needed for factories, as we can't statically analyze the converter type they return.
    /// </summary>
    public required bool WrapInNullable { get; init; }

    /// <summary>
    /// Whether the converter is a factory.
    /// </summary>
    public required bool IsFactory { get; init; }

    /// <summary>
    /// Returns a converter override, or null.
    /// </summary>
    public static IConverterModel? Create(
        ISymbol propertyOrParameter,
        ITypeSymbol convertedType,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector
    )
    {
        ITypeSymbol? converter = null;
        ITypeSymbol? converterActualTValue = null;
        AttributeData? matchingAttribute = null;

        foreach (AttributeData attribute in propertyOrParameter.GetAttributes())
        {
            if (attribute.TryGetFlameCsvAttribute(out var attrSymbol) is false)
            {
                continue;
            }

            if (symbols.IsStringPoolingAttribute(attrSymbol))
            {
                var memberType = propertyOrParameter switch
                {
                    IPropertySymbol property => property.Type,
                    IFieldSymbol field => field.Type,
                    IParameterSymbol parameter => parameter.Type,
                    _ => null,
                };

                if (memberType is not { SpecialType: SpecialType.System_String })
                {
                    collector.AddDiagnostic(
                        Diagnostics.CsvConverterTypeMismatch(
                            target: symbols.TargetType,
                            converterType: StringPoolingConverterModel.GetName(symbols.TokenType.Name, full: false),
                            expectedType: "string",
                            actualType: memberType?.ToDisplayString() ?? "<unknown>",
                            location: attribute.GetLocation()
                        )
                    );
                }

                return StringPoolingConverterModel.Instance;
            }

            if (
                attrSymbol is { IsGenericType: true, Arity: 1 }
                && symbols.IsCsvConverterOfTAttribute(attrSymbol.ConstructUnboundGenericType())
                && IsConverterOrFactory(
                    attrSymbol.TypeArguments[0],
                    symbols.TokenType,
                    in symbols,
                    out converterActualTValue
                )
            )
            {
                matchingAttribute = attribute;
                converter = attrSymbol.TypeArguments[0];
                break;
            }
        }

        // no converter override
        if (converter is null)
        {
            return null;
        }

        TypeRef converterType = new(converter);
        ConstructorArgumentType constructorArguments = default;
        bool isFactory = converterActualTValue is null; // if the converter is a factory, it has no TValue type argument

        foreach (var ctor in converter.GetInstanceConstructors())
        {
            if (ctor.Parameters.IsEmpty)
            {
                // don't break, we might find a better constructor
                constructorArguments = ConstructorArgumentType.Empty;
                continue;
            }

            if (ctor.Parameters is [var singleParam] && symbols.IsCsvOptionsOfT(singleParam.Type))
            {
                constructorArguments = ConstructorArgumentType.Options;
                break; // we found the best constructor
            }
        }

        bool wrapInNullable = false;

        // check if the concrete type is not an exact match
        // leave factories as they are since we can't statically analyze if they support nullable types
        if (!isFactory && !SymbolEqualityComparer.Default.Equals(convertedType, converterActualTValue))
        {
            // wrap in nullable if needed, e.g., property is 'int?' but the converter is for int
            if (
                convertedType.IsNullable(out ITypeSymbol? underlyingType)
                && SymbolEqualityComparer.Default.Equals(underlyingType, converterActualTValue)
            )
            {
                wrapInNullable = true;
            }
            else
            {
                collector.AddDiagnostic(
                    Diagnostics.CsvConverterTypeMismatch(
                        target: symbols.TargetType,
                        converterType: converter.ToDisplayString(),
                        expectedType: convertedType.ToDisplayString(),
                        actualType: converterActualTValue!.ToDisplayString(),
                        location: matchingAttribute?.GetLocation()
                    )
                );

                // don't return null so the incremental caching doesn't confuse between missing and invalid config
            }
        }

        if (constructorArguments is ConstructorArgumentType.Invalid)
        {
            collector.AddDiagnostic(
                Diagnostics.NoCsvConverterConstructor(converter, converterType.Name, convertedType)
            );
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

    /// <summary>
    /// Determines if the type is a converter or factory for the given token type.
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <param name="tokenType">Token type (char or byte)</param>
    /// <param name="symbols">Symbols to check against</param>
    /// <param name="convertedType">Converted type if the type is <c>CsvConverter&lt;T, TResult&gt;</c></param>
    /// <returns></returns>
    public static bool IsConverterOrFactory(
        ITypeSymbol? type,
        ITypeSymbol tokenType,
        ref readonly FlameSymbols symbols,
        out ITypeSymbol? convertedType
    )
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
                        convertedType = genericType.TypeArguments[1];
                        return true;
                    }
                }
                else if (genericType.Arity == 1)
                {
                    if (symbols.IsCsvConverterFactoryOfT(genericType))
                    {
                        convertedType = null;
                        return true;
                    }
                }
            }

            type = type.BaseType;
        }

        convertedType = null;
        return false;
    }

    public bool Equals(IConverterModel other) => Equals(other as ConverterModel);
}
