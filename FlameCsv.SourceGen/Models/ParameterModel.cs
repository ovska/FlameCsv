using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record ParameterModel : IComparable<ParameterModel> // TODO IEquatable
{
    public required TypeRef ParameterType { get; init; }
    public required string Name { get; init; }
    public required bool HasDefaultValue { get; init; }

    // The default value of a constructor parameter can only be a constant
    // so it always satisfies the structural equality requirement for the record.
    public required object? DefaultValue { get; init; }
    public required int ParameterIndex { get; init; }

    public static bool TryCreate(
        ISymbol symbol,
        KnownSymbols knownSymbols,
        out ImmutableEquatableArray<ParameterModel> model)
    {
        // TODO
        if (symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor, DeclaredAccessibility: not Accessibility.Private } ctor)
            goto Fail;

        if (ctor.Parameters.Length == 0 || knownSymbols.Nullable)
        {
            model = [];
            return false;
        }

        Fail:
        model = [];
        return false;
    }

    public int CompareTo(ParameterModel other) => ParameterIndex.CompareTo(other.ParameterIndex);
}
