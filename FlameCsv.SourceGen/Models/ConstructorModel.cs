using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal readonly struct ConstructorModel
{
    public ITypeSymbol TargetType { get; init; }
    public AttributeData Attribute { get; init; }
    public ImmutableArray<TypedConstant> Types { get; init; }

    public static ConstructorModel? TryParseConstructorAttribute(
        bool isOnAssembly,
        ITypeSymbol targetType,
        AttributeData attribute,
        ref readonly FlameSymbols symbols)
    {
        if (!symbols.IsCsvConstructorAttribute(attribute.AttributeClass)) return null;

        bool needType = isOnAssembly;
        ImmutableArray<TypedConstant> types = default;

        foreach (var arg in attribute.NamedArguments)
        {
            if (arg is { Key: "ParameterTypes", Value.Kind: TypedConstantKind.Array })
            {
                types = arg.Value.Values;
            }
            else if (needType && arg is { Key: "TargetType", Value.Kind: TypedConstantKind.Type })
            {
                if (!SymbolEqualityComparer.Default.Equals(targetType, arg.Value.Value as ITypeSymbol))
                {
                    return null;
                }

                needType = false;
            }
        }

        if (types.IsDefault || needType)
        {
            return null;
        }

        return new ConstructorModel
        {
            TargetType = targetType,
            Attribute = attribute,
            Types = types,
        };
    }

    public static EquatableArray<ParameterModel> ParseConstructor(
        ITypeSymbol targetType,
        ITypeSymbol tokenSymbol,
        ConstructorModel? typeConstructorAttribute,
        CancellationToken cancellationToken,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ImmutableArray<IMethodSymbol> constructors = targetType.GetInstanceConstructors();
        IMethodSymbol? constructor = null;

        // [CsvConstructor] on type or assembly
        if (typeConstructorAttribute is { } typeAttr)
        {
            foreach (var ctor in constructors)
            {
                if (ctor.Parameters.Length == typeAttr.Types.Length)
                {
                    bool match = true;

                    for (int i = 0; i < ctor.Parameters.Length; i++)
                    {
                        if (!SymbolEqualityComparer.Default.Equals(
                                ctor.Parameters[i].Type,
                                typeAttr.Types[i].Value as ITypeSymbol))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        constructor = ctor;
                        break;
                    }
                }
            }

            // there was a [CsvConstructor] on the type or assembly, but no matching constructor was found
            if (constructor is null)
            {
                collector.AddDiagnostic(
                    Diagnostics.NoMatchingConstructor(
                        targetType,
                        typeAttr.Types.Select(t => t.Value as ITypeSymbol),
                        typeAttr.Attribute.GetLocation()));

                MarkParametersAsHandled(ref collector);
                return [];
            }
        }
        // use a single public ctor if available
        else if (constructors.Length == 1)
        {
            constructor = constructors[0];
        }
        // use either parameterless if available, or ctor annotated with [CsvConstructor]
        else
        {
            foreach (var ctor in constructors)
            {
                if (ctor.Parameters.IsDefaultOrEmpty)
                {
                    constructor ??= ctor;
                    continue;
                }

                foreach (var attr in ctor.GetAttributes())
                {
                    if (symbols.IsCsvConstructorAttribute(attr.AttributeClass))
                    {
                        constructor = ctor;
                    }
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // check if the constructor is not null and is accessible
        if (constructor is { DeclaredAccessibility: not (Accessibility.Private or Accessibility.Protected) })
        {
            return ParameterModel.Create(
                tokenSymbol,
                targetType,
                constructor,
                in symbols,
                ref collector);
        }

        collector.AddDiagnostic(Diagnostics.NoValidConstructor(targetType, constructor));
        MarkParametersAsHandled(ref collector);
        return [];
    }

    /// <summary>
    /// Marks all parameters as handled as the constructor is not valid we can't know about the validity of the params.
    /// </summary>
    private static void MarkParametersAsHandled(ref AnalysisCollector collector)
    {
        foreach (ref readonly var targetAttribute in collector.TargetAttributes.WrittenSpan)
        {
            if (targetAttribute.IsParameter)
            {
                targetAttribute.MatchFound = true;
            }
        }
    }
}
