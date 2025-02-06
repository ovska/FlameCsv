using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal readonly struct ConstructorModel
{
    public ITypeSymbol TargetType { get; init; }
    public AttributeData Attribute { get; init; }
    public ImmutableArray<TypedConstant> Types { get; init; }

    public static ConstructorModel? ParseConstructorAttribute(ITypeSymbol targetType, AttributeData attribute)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == "ParameterTypes")
            {
                return new ConstructorModel
                {
                    TargetType = targetType,
                    Attribute = attribute,
                    Types = arg.Value.Values,
                };
            }
        }

        return null;
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

            if (constructor is null)
            {
                var syntaxRef = typeAttr.Attribute.ApplicationSyntaxReference;

                collector.AddDiagnostic(
                    Diagnostics.NoMatchingConstructor(
                        targetType,
                        typeAttr.Types.Select(t => t.Value).OfType<ITypeSymbol>(),
                        syntaxRef?.SyntaxTree.GetLocation(syntaxRef.Span)));

                return [];
            }
        }
        else if (constructors.Length == 1)
        {
            constructor = constructors[0];
        }
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
        return [];
    }
}
