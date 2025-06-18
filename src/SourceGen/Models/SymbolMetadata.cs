using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

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

    /// <summary>
    /// Creates a new instance of <see cref="SymbolMetadata"/> from the given symbol and attributes.
    /// </summary>
    /// <param name="symbolActualName">
    /// Name of the symbol, or the name of the original member if this is an explicitly implemented property.
    /// </param>
    /// <param name="symbol">Field, property, or parameter symbol to extract metadata from.</param>
    /// <param name="symbols">FlameCsv symbols, used to check for attribute types.</param>
    /// <param name="collector">Collector to report diagnostics and conflicts to.</param>
    public SymbolMetadata(
        string symbolActualName,
        ISymbol symbol,
        ref readonly FlameSymbols symbols,
        ref AnalysisCollector collector
    )
    {
        HashSet<string>? aliasSet = null;

        // keep track of conflicts
        List<string>? configNames = null;
        List<Location?>? locations = null;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrSymbol)
            {
                continue;
            }

            if (symbols.IsCsvHeaderAttribute(attrSymbol))
            {
                AttributeConfiguration.ParseHeader(attribute, out var headerName, out var aliases);
                HeaderName = TryGetHeaderName(HeaderName, headerName, attribute, ref configNames, ref locations);
                AddAliases(ref aliasSet, aliases);
            }
            else if (symbols.IsCsvRequiredAttribute(attrSymbol))
            {
                IsRequired = true;
            }
            else if (symbols.IsCsvOrderAttribute(attrSymbol))
            {
                AttributeConfiguration.ParseOrder(attribute, out var order);
                Order = TryGetOrder(Order, order, attribute, ref configNames, ref locations);
            }
            else if (symbols.IsCsvIndexAttribute(attrSymbol))
            {
                AttributeConfiguration.ParseIndex(attribute, out var index);
                Index = TryGetIndex(Index, index, attribute, ref configNames, ref locations);
            }
            else if (symbols.IsCsvIgnoreAttribute(attrSymbol))
            {
                IsIgnored = true;
            }
        }

        bool isParameter = symbol.Kind == SymbolKind.Parameter;

        foreach (ref readonly var targeted in collector.TargetAttributes.WrittenSpan)
        {
            if (targeted.IsParameter != isParameter || targeted.MemberName != symbolActualName)
            {
                continue;
            }

            targeted.MatchFound = true;

            HeaderName = TryGetHeaderName(
                HeaderName,
                targeted.HeaderName,
                targeted.Attribute,
                ref configNames,
                ref locations
            );
            AddAliases(ref aliasSet, targeted.Aliases);
            Order = TryGetOrder(Order, targeted.Order, targeted.Attribute, ref configNames, ref locations);
            Index = TryGetIndex(Index, targeted.Index, targeted.Attribute, ref configNames, ref locations);
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
                        location: locations[i]
                    )
                );
            }
        }

        PooledList<string>.Release(configNames);
        PooledList<Location?>.Release(locations);
        Aliases = aliasSet.ToEquatableArrayAndFree();
    }

    private static void AddAliases(ref HashSet<string>? aliasSet, ImmutableArray<TypedConstant> aliases)
    {
        if (aliases.IsDefaultOrEmpty)
            return;

        foreach (var alias in aliases)
        {
            if (alias.Value?.ToString() is { } value)
            {
                (aliasSet ??= PooledSet<string>.Acquire()).Add(value);
            }
        }
    }

    private static int? TryGetIndex(
        int? existing,
        int? index,
        AttributeData attribute,
        ref List<string>? configNames,
        ref List<Location?>? locations
    )
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

    private static int? TryGetOrder(
        int? existing,
        int? order,
        AttributeData attribute,
        ref List<string>? configNames,
        ref List<Location?>? locations
    )
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

    private static string? TryGetHeaderName(
        string? existing,
        string? headerName,
        AttributeData attribute,
        ref List<string>? configNames,
        ref List<Location?>? locations
    )
    {
        if (headerName is not null && existing is not null && existing != headerName)
        {
            (configNames ??= PooledList<string>.Acquire()).Add("HeaderName");
            (locations ??= PooledList<Location?>.Acquire()).Add(attribute.GetLocation());
        }

        return headerName;
    }
}
