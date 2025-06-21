using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal interface IConverterModel : IEquatable<IConverterModel>;

internal sealed class StringPoolingConverterModel : IConverterModel
{
    public static readonly StringPoolingConverterModel Instance = new();

    private StringPoolingConverterModel() { }

    public bool Equals(IConverterModel? other) => other is StringPoolingConverterModel;

    public string GetName(bool isByte) =>
        isByte
            ? "global::FlameCsv.Converters.CsvPoolingStringUtf8Converter"
            : "global::FlameCsv.Converters.CsvPoolingStringTextConverter";
}

// this is a class as it should be relatively rare, and would needlessly take space in other large structs
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

        foreach (AttributeData attribute in propertyOrParameter.GetAttributes())
        {
            if (attribute.TryGetFlameCsvAttribute(out var attrSymbol) is false)
            {
                continue;
            }

            if (
                symbols.IsStringPoolingAttribute(attrSymbol)
                && propertyOrParameter.GetMemberType() is { SpecialType: SpecialType.System_String }
            )
            {
                return StringPoolingConverterModel.Instance;
            }

            if (
                attrSymbol is { IsGenericType: true, Arity: 1 }
                && symbols.IsCsvConverterOfTAttribute(attrSymbol.ConstructUnboundGenericType())
                && IsConverterOrFactory(attrSymbol.TypeArguments[0], symbols.TokenType, in symbols)
            )
            {
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

    public bool Equals(IConverterModel other) => Equals(other as ConverterModel);
}
