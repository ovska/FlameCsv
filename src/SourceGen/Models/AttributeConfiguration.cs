using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal readonly struct AttributeConfiguration(AttributeData attribute)
{
    public override string ToString() => $"TargetAttribute '{MemberName}'{(IsParameter ? " (parameter)" : "")}";

    public required string MemberName { get; init; }
    public required bool IsParameter { get; init; }
    public required bool IsIgnored { get; init; }
    public required bool IsRequired { get; init; }
    public required int? Order { get; init; }
    public required int? Index { get; init; }
    public required string? HeaderName { get; init; }
    public required ImmutableArray<TypedConstant> Aliases { get; init; }

    /// <summary>
    /// Whether a parameter or member with name <see cref="MemberName"/> was found.
    /// </summary>
    public bool MatchFound
    {
        get => _matchFound.Value;
        set => _matchFound.Value = value;
    }

    private readonly StrongBox<bool> _matchFound = new();

    public AttributeData Attribute => attribute;

    public static AttributeConfiguration? TryCreate(
        ITypeSymbol targetType,
        bool isOnAssembly,
        AttributeData attribute,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector
    )
    {
        if (attribute.AttributeClass is not { } attrSymbol)
            return null;

        string? memberName;
        bool isParameter = false;

        bool isIgnored = false;
        bool isRequired = false;
        int? order = null;
        int? index = null;
        string? headerName = null;
        ImmutableArray<TypedConstant> aliases = default;

        if (symbols.IsCsvHeaderAttribute(attrSymbol))
        {
            if (!TryGetMemberName(ref collector, out memberName))
                return null;
            ParseHeader(attribute, out headerName, out aliases);
        }
        else if (symbols.IsCsvRequiredAttribute(attrSymbol))
        {
            if (!TryGetMemberName(ref collector, out memberName))
                return null;
            isRequired = true;
        }
        else if (symbols.IsCsvOrderAttribute(attrSymbol))
        {
            if (!TryGetMemberName(ref collector, out memberName))
                return null;
            ParseOrder(attribute, out order);
        }
        else if (symbols.IsCsvIndexAttribute(attrSymbol))
        {
            if (!TryGetMemberName(ref collector, out memberName))
                return null;
            ParseIndex(attribute, out index);
        }
        else if (symbols.IsCsvIgnoreAttribute(attrSymbol))
        {
            if (!TryGetMemberName(ref collector, out memberName))
                return null;
            isIgnored = true;
        }
        else if (symbols.IsIgnoredIndexesAttribute(attrSymbol))
        {
            if (!TryEnsureType(ref collector))
                return null;

            ImmutableArray<TypedConstant> indexes = [];

            if (attribute.ConstructorArguments.Length > 0)
            {
                indexes = attribute.ConstructorArguments[0].Values;
            }
            else if (attribute.TryGetNamedArgument("Value", out var value))
            {
                indexes = value.Values;
            }

            foreach (var value in indexes)
            {
                if (value is { Kind: TypedConstantKind.Primitive, Value: int ignoredIndex and >= 0 })
                {
                    collector.IgnoredIndexes.Add(ignoredIndex);
                }
            }

            return null;
        }
        else if (symbols.IsTypeProxyAttribute(attrSymbol))
        {
            if (!TryEnsureType(ref collector))
                return null;

            if (attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Type, Value: ITypeSymbol proxy })
            {
                collector.AddProxy(proxy, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
            }

            return null;
        }
        else
        {
            return null;
        }

        return new AttributeConfiguration(attribute)
        {
            MemberName = memberName,
            IsParameter = isParameter,
            IsIgnored = isIgnored,
            IsRequired = isRequired,
            Order = order,
            Index = index,
            HeaderName = headerName,
            Aliases = aliases.IsDefault ? [] : aliases,
        };

        bool TryEnsureType(ref AnalysisCollector collector)
        {
            if (!isOnAssembly)
            {
                return true;
            }

            if (!attribute.TryGetNamedArgument("TargetType", out TypedConstant targetTypeArg))
            {
                collector.AddDiagnostic(Diagnostics.NoTargetTypeOnAssembly(attribute, attrSymbol));
                return false;
            }

            if (!SymbolEqualityComparer.Default.Equals(targetType, targetTypeArg.Value as ISymbol))
            {
                // for another type
                return false;
            }

            return true;
        }

        bool TryGetMemberName(ref AnalysisCollector collector, [NotNullWhen(true)] out string? result)
        {
            if (!TryEnsureType(ref collector))
            {
                result = null;
                return false;
            }

            result = null;

            foreach (var arg in attribute.NamedArguments)
            {
                if (arg.Key == "MemberName")
                {
                    result = arg.Value.Value?.ToString();
                }
                else if (arg.Key == "IsParameter")
                {
                    isParameter = arg.Value.Value is true;
                }
            }

            if (result is null)
            {
                collector.AddDiagnostic(Diagnostics.NoMemberNameOnAttribute(attribute, isOnAssembly));
                return false;
            }

            return true;
        }
    }

    public static void ParseIndex(AttributeData attribute, out int? index)
    {
        index = null;

        if (attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Primitive, Value: int indexArg })
        {
            index = indexArg;
        }
    }

    public static void ParseOrder(AttributeData attribute, out int? order)
    {
        order = null;

        if (attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Primitive, Value: int orderArg })
        {
            order = orderArg;
        }
    }

    public static void ParseHeader(
        AttributeData attribute,
        out string? headerName,
        out ImmutableArray<TypedConstant> aliases
    )
    {
        aliases = default;

        headerName =
            attribute.ConstructorArguments is [var value, ..] ? value.Value?.ToString()
            : attribute.TryGetNamedArgument("Value", out value) ? value.Value?.ToString()
            : null;

        if (
            attribute.ConstructorArguments.Length > 1
            && attribute.ConstructorArguments[1] is { Kind: TypedConstantKind.Array } arr
        )
        {
            aliases = arr.Values;
        }
        else if (
            attribute.TryGetNamedArgument("Aliases", out var aliasesArg)
            && aliasesArg.Kind == TypedConstantKind.Array
        )
        {
            aliases = aliasesArg.Values;
        }
    }

    [ExcludeFromCodeCoverage]
    public override bool Equals(object? obj) => throw new NotSupportedException();

    [ExcludeFromCodeCoverage]
    public override int GetHashCode() => throw new NotSupportedException();
}
