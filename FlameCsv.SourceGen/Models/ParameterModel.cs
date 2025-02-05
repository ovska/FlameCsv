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
    /// Identifier of the parameter (name prefixed with <c>p_</c>).
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Configured header names.
    /// </summary>
    public required EquatableArray<string> Names { get; init; }

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

    /// <summary>
    /// Whether the parameter is ignored.
    /// </summary>
    public required bool IsIgnored { get; init; }

    /// <summary>
    /// Required to match to a header when reading CSV.
    /// </summary>
    public bool IsRequired => IsRequiredByAttribute || !HasDefaultValue;

    bool IMemberModel.CanRead => true;
    bool IMemberModel.CanWrite => false;
    TypeRef IMemberModel.Type => ParameterType;

    public void WriteId(StringBuilder sb)
    {
        sb.Append("@s__p_Id_");
        sb.Append(Name);
    }

    public void WriteConverterName(StringBuilder sb)
    {
        sb.Append("@s__p_Converter_");
        sb.Append(Name);
    }

    public static EquatableArray<ParameterModel> Create(
        ITypeSymbol token,
        ITypeSymbol targetType,
        IMethodSymbol constructor,
        CancellationToken cancellationToken,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector)
    {
        List<ParameterModel> models = PooledList<ParameterModel>.Acquire();

        ImmutableArray<IParameterSymbol> parameters = constructor.Parameters;

        for (int index = 0; index < parameters.Length; index++)
        {
            IParameterSymbol parameter = parameters[index];
            SymbolMetadata meta = new(parameter.Name, parameter, cancellationToken, in symbols, ref collector);

            ParameterModel parameterModel = new()
            {
                ParameterIndex = index,
                ParameterType = new TypeRef(parameter.Type),
                Name = parameter.Name,
                Identifier = "p_" + parameter.Name,
                Names = meta.Names,
                Order = meta.Order ?? 0,
                IsIgnored = meta.IsIgnored ?? false,
                IsRequiredByAttribute = meta.IsRequired ?? false,
                HasDefaultValue = parameter.HasExplicitDefaultValue,
                DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null,
                RefKind = parameter.RefKind,
                OverriddenConverter = ConverterModel.Create(
                    token,
                    parameter,
                    parameter.Type,
                    in symbols,
                    ref collector)
            };
            models.Add(parameterModel);

            if (parameter.RefKind is not (RefKind.None or RefKind.In or RefKind.RefReadOnlyParameter))
            {
                collector.AddDiagnostic(
                    Diagnostics.RefConstructorParameter(
                        targetType,
                        constructor,
                        constructor.Parameters[index]));
            }

            if (parameter.Type.IsRefLikeType)
            {
                collector.AddDiagnostic(
                    Diagnostics.RefLikeConstructorParameter(
                        targetType,
                        constructor,
                        constructor.Parameters[index]));
            }

            if (meta.IsIgnored is true && !parameter.HasExplicitDefaultValue)
            {
                collector.AddDiagnostic(Diagnostics.IgnoredParameterWithoutDefaultValue(parameter, targetType));
            }
        }

        models.Sort();
        return models.ToEquatableArrayAndFree();
    }

    public int CompareTo(ParameterModel other) => ParameterIndex.CompareTo(other.ParameterIndex);

    public bool Equals(IMemberModel? other) => Equals(other as ParameterModel);
}
