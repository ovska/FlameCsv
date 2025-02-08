using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

/// <summary>
/// Contains attribute data from a property, field, or parameter.
/// </summary>
// ref struct to avoid accidental storage
internal readonly ref struct SymbolMetadata
{
    public string? HeaderName { get; }
    public EquatableArray<string> Aliases { get; }
    public bool IsRequired { get; }
    public bool IsIgnored { get; }
    public int? Order { get; }
    public int? Index { get; }

    public SymbolMetadata(
        string symbolActualName,
        ISymbol symbol,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector)
    {
        HashSet<string>? aliasSet = null;

        // keep track of conflicts
        List<string>? configNames = null;
        List<Location?>? locations = null;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrSymbol) continue;

            if (symbols.IsCsvHeaderAttribute(attrSymbol))
            {
                AttributeConfiguration.ParseHeader(attribute, out var headerName, out var aliases);
                HeaderName = TryGetHeaderName(HeaderName, headerName, attribute);
                AddAliases(ref aliasSet, aliases);
            }
            else if (symbols.IsCsvRequiredAttribute(attrSymbol))
            {
                IsRequired = true;
            }
            else if (symbols.IsCsvOrderAttribute(attrSymbol))
            {
                AttributeConfiguration.ParseOrder(attribute, out var order);
                Order = TryGetOrder(Order, order, attribute);
            }
            else if (symbols.IsCsvIndexAttribute(attrSymbol))
            {
                AttributeConfiguration.ParseIndex(attribute, out var index);
                Index = TryGetIndex(Index, index, attribute);
            }
            else if (symbols.IsCsvIgnoreAttribute(attrSymbol))
            {
                IsIgnored = true;
            }
        }

        bool isParameter = symbol.Kind == SymbolKind.Parameter;

        foreach (var targeted in collector.TargetAttributes)
        {
            if (targeted.IsParameter != isParameter || targeted.MemberName != symbolActualName)
            {
                continue;
            }

            targeted.MatchFound = true;

            HeaderName ??= TryGetHeaderName(HeaderName, targeted.HeaderName, targeted.Attribute);
            AddAliases(ref aliasSet, targeted.Aliases);
            Order ??= TryGetOrder(Order, targeted.Order, targeted.Attribute);
            Index ??= TryGetIndex(Index, targeted.Index, targeted.Attribute);
            IsIgnored |= targeted.IsIgnored;
            IsRequired |= targeted.IsRequired;
        }

        // report conflicts
        if (configNames is not null && locations is not null)
        {
            for (int i = 0; i < configNames.Count; i++)
            {
                collector.AddDiagnostic(
                    Diagnostics.ConflictingConfiguration(
                        targetType: symbols.TargetType,
                        memberType: isParameter ? "parameter" : "property/field",
                        memberName: symbol.Name,
                        configurationName: configNames[i],
                        location: locations[i]));
            }
        }

        PooledList<string>.Release(configNames);
        PooledList<Location?>.Release(locations);
        Aliases = aliasSet?.ToEquatableArrayAndFree() ?? [];

        static void AddAliases(ref HashSet<string>? aliasSet, ImmutableArray<TypedConstant> aliases)
        {
            if (aliases.IsDefault) return;

            foreach (var alias in aliases)
            {
                if (alias.Value?.ToString() is { } value)
                {
                    (aliasSet ??= PooledSet<string>.Acquire()).Add(value);
                }
            }
        }

        string? TryGetHeaderName(string? existing, string? headerName, AttributeData attribute)
        {
            if (headerName is not null)
            {
                if (existing is not null && existing != headerName)
                {
                    (configNames ??= PooledList<string>.Acquire()).Add("HeaderName");
                    (locations ??= PooledList<Location?>.Acquire()).Add(attribute.GetLocation());
                }
            }

            return headerName;
        }

        int? TryGetOrder(int? existing, int? order, AttributeData attribute)
        {
            if (order.HasValue)
            {
                if (existing.HasValue && existing.Value != order.Value)
                {
                    (configNames ??= PooledList<string>.Acquire()).Add("Order");
                    (locations ??= PooledList<Location?>.Acquire()).Add(attribute.GetLocation());
                }
            }

            return order;
        }

        int? TryGetIndex(int? existing, int? index, AttributeData attribute)
        {
            if (index.HasValue)
            {
                if (existing.HasValue && existing.Value != index.Value)
                {
                    (configNames ??= PooledList<string>.Acquire()).Add("Index");
                    (locations ??= PooledList<Location?>.Acquire()).Add(attribute.GetLocation());
                }
            }

            return index;
        }
    }
}
