using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record ParameterModel : IComparable<ParameterModel>, IMemberModel
{
    /// <summary>
    /// 0-based index of the parameter in the constructor.
    /// </summary>
    public required int ParameterIndex { get; init; }

    /// <summary>
    /// Parameter type.
    /// </summary>
    public required TypeRef ParameterType { get; init; }

    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Configured header names, always non-empty.
    /// </summary>
    public required ImmutableEquatableArray<string> Names { get; init; }

    /// <summary>
    /// Whether the parameter is required by an attribute.
    /// </summary>
    public required bool IsRequiredByAttribute { get; init; }

    /// <summary>
    /// Whether the parameter has a default value.
    /// </summary>
    public required bool HasDefaultValue { get; init; }

    /// <summary>
    /// Default value of the parameter.
    /// </summary>
    /// <remarks>
    /// The default value of a constructor parameter can only be a constant,
    /// so it always satisfies the structural equality requirement for the record.
    /// </remarks>
    public required object? DefaultValue { get; init; }

    /// <summary>
    /// Ref kind of the parameter.
    /// </summary>
    public required RefKind RefKind { get; init; }

    /// <summary>
    /// Overridden converter for this parameter.
    /// </summary>
    public required ConverterModel? OverriddenConverter { get; init; }

    /// <summary>
    /// Order of the parameter.
    /// </summary>
    public required int Order { get; init; }

    public bool IsRequired => IsRequiredByAttribute || !HasDefaultValue;

    CsvBindingScope IMemberModel.Scope => CsvBindingScope.Read;
    bool IMemberModel.CanRead => false;
    bool IMemberModel.CanWrite => true;
    string IMemberModel.IndexPrefix => "@s__p_Index_";
    string IMemberModel.ConverterPrefix => "@s__p_Converter_";
    TypeRef IMemberModel.Type => ParameterType;

    public static bool IsValidConstructor(ISymbol symbol, [NotNullWhen(true)] out IMethodSymbol? ctor)
    {
        if (symbol is IMethodSymbol
            {
                MethodKind: MethodKind.Constructor,
                DeclaredAccessibility:
                Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal
                or Accessibility.NotApplicable
            } constructor)
        {
            ctor = constructor;
            return true;
        }

        ctor = null;
        return false;
    }

    public static ImmutableSortedArray<ParameterModel> Create(
        ITypeSymbol token,
        ImmutableArray<IParameterSymbol> parameters,
        FlameSymbols symbols,
        List<Diagnostic> diagnostics)
    {
        List<ParameterModel> models = new(parameters.Length);

        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            var meta = new SymbolMetadata(token, parameter, symbols);

            ParameterModel parameterModel = new()
            {
                ParameterIndex = i,
                ParameterType = new TypeRef(parameter.Type),
                Name = parameter.Name,
                Names = meta.Names.ToImmutableEquatableArray(),
                Order = meta.Order,
                IsRequiredByAttribute = meta.IsRequired,
                HasDefaultValue = parameter.HasExplicitDefaultValue,
                DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null,
                RefKind = parameter.RefKind,
                OverriddenConverter = ConverterModel.GetOverriddenConverter(
                    token,
                    parameter,
                    parameter.Type,
                    symbols)
            };
            models.Add(parameterModel);

            if (parameterModel.OverriddenConverter is { ConstructorArguments: ConstructorArgumentType.Invalid })
            {
                diagnostics.Add(
                    Diagnostics.NoCsvFactoryConstructorFound(
                        parameter,
                        parameterModel.OverriddenConverter.ConverterType.FullyQualifiedName,
                        token));
            }
        }

        return models.ToImmutableSortedArray();
    }

    public int CompareTo(ParameterModel other) => ParameterIndex.CompareTo(other.ParameterIndex);
}
