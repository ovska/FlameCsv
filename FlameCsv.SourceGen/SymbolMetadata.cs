using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

/// <summary>
/// Contains attribute data from a property, field, or parameter.
/// </summary>
// ref struct to avoid accidental storage
internal readonly ref struct SymbolMetadata
{
    public EquatableArray<string> Names { get; }
    public bool? IsRequired { get; }
    public bool? IsIgnored { get; }
    public int? Order { get; }
    public int? Index { get; }

    public Location? GetLocation(CancellationToken cancellationToken)
        => _attributeSyntax?.GetSyntax(cancellationToken).GetLocation();

    private readonly SyntaxReference? _attributeSyntax;

    public SymbolMetadata(
        string symbolActualName,
        ISymbol symbol,
        CancellationToken cancellationToken,
        ref readonly FlameSymbols flameSymbols,
        ref AnalysisCollector collector)
    {
        AttributeData? attribute = null;

        foreach (var attributeData in symbol.GetAttributes())
        {
            if (flameSymbols.IsCsvFieldAttribute(attributeData.AttributeClass))
            {
                attribute = attributeData;
                break;
            }
        }

        HashSet<string> nameSet = PooledSet<string>.Acquire();

        if (attribute is not null)
        {
            _attributeSyntax = attribute.ApplicationSyntaxReference;

            foreach (var value in attribute.ConstructorArguments[0].Values)
            {
                if (value.Value?.ToString() is { Length: > 0 } headerName)
                {
                    nameSet.Add(headerName);
                }
            }

            foreach (var argument in attribute.NamedArguments)
            {
                switch (argument.Key)
                {
                    case "IsIgnored":
                        IsIgnored = argument.Value.Value is true;
                        break;
                    case "IsRequired":
                        IsRequired = argument.Value.Value is true;
                        break;
                    case "Order":
                        Order = argument.Value.Value as int? ?? 0;
                        break;
                    case "Index":
                        Index = argument.Value.Value as int? ?? 0;
                        break;
                }
            }
        }

        bool isParameter = symbol.Kind == SymbolKind.Parameter;

        // keep track of targeted conflicts
        List<string>? names = null;
        List<Location?>? locations = null;

        foreach (var targeted in collector.TargetAttributes)
        {
            if (targeted.IsParameter != isParameter || targeted.MemberName != symbolActualName)
            {
                continue;
            }

            targeted.MatchFound = true;

            foreach (var name in targeted.Names)
            {
                if (name.Value?.ToString() is { } headerName)
                {
                    nameSet.Add(headerName);
                }
            }

            if (targeted.IsIgnored.HasValue)
            {
                if (IsIgnored.HasValue && IsIgnored.Value != targeted.IsIgnored)
                {
                    (names ??= PooledList<string>.Acquire()).Add("IsIgnored");
                    (locations ??= PooledList<Location?>.Acquire()).Add(targeted.GetLocation(cancellationToken));
                }

                IsIgnored |= targeted.IsIgnored;
            }

            if (targeted.IsRequired.HasValue)
            {
                if (IsRequired.HasValue && IsRequired.Value != targeted.IsRequired)
                {
                    (names ??= PooledList<string>.Acquire()).Add("IsRequired");
                    (locations ??= PooledList<Location?>.Acquire()).Add(targeted.GetLocation(cancellationToken));
                }

                IsRequired |= targeted.IsRequired;
            }

            if (targeted.Order.HasValue)
            {
                if (Order.HasValue && Order.Value != targeted.Order)
                {
                    (names ??= PooledList<string>.Acquire()).Add("Order");
                    (locations ??= PooledList<Location?>.Acquire()).Add(targeted.GetLocation(cancellationToken));
                }

                Order = targeted.Order;
            }

            if (targeted.Index.HasValue)
            {
                if (Index.HasValue && Index.Value != targeted.Index)
                {
                    (names ??= PooledList<string>.Acquire()).Add("Index");
                    (locations ??= PooledList<Location?>.Acquire()).Add(targeted.GetLocation(cancellationToken));
                }

                Index = targeted.Index;
            }
        }

        if (names is not null && locations is not null)
        {
            for (int i = 0; i < names.Count; i++)
            {
                collector.AddDiagnostic(
                    Diagnostics.ConflictingConfiguration(
                        targetType: flameSymbols.TargetType,
                        memberType: isParameter ? "parameter" : "property/field",
                        memberName: symbol.Name,
                        configurationName: names[i],
                        location: locations[i],
                        additionalLocation: GetLocation(cancellationToken)));
            }
        }

        PooledList<string>.Release(names);
        PooledList<Location?>.Release(locations);
        Names = nameSet.ToEquatableArrayAndFree();
    }
}
