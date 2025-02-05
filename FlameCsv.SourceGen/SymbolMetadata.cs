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

        if (attribute is null)
        {
            this = default;
            return;
        }

        _attributeSyntax = attribute.ApplicationSyntaxReference;

        HashSet<string> nameSet = PooledSet<string>.Acquire();

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

        bool isParameter = symbol.Kind == SymbolKind.Parameter;

        List<string> conflictNames = PooledList<string>.Acquire();
        List<Location?> conflictLocations = PooledList<Location?>.Acquire();

        foreach (var targeted in collector.TargetAttributes)
        {
            if (targeted.IsParameter != isParameter ||
                targeted.MemberName != symbol.Name)
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
                    conflictNames.Add("IsIgnored");
                    conflictLocations.Add(targeted.GetLocation(cancellationToken));
                }

                IsIgnored |= targeted.IsIgnored;
            }

            if (targeted.IsRequired.HasValue)
            {
                if (IsRequired.HasValue && IsRequired.Value != targeted.IsRequired)
                {
                    conflictNames.Add("IsRequired");
                    conflictLocations.Add(targeted.GetLocation(cancellationToken));
                }

                IsRequired |= targeted.IsRequired;
            }

            if (targeted.Order.HasValue)
            {
                if (Order.HasValue && Order.Value != targeted.Order)
                {
                    conflictNames.Add("Order");
                    conflictLocations.Add(targeted.GetLocation(cancellationToken));
                }

                Order = targeted.Order;
            }

            if (targeted.Index.HasValue)
            {
                if (Index.HasValue && Index.Value != targeted.Index)
                {
                    conflictNames.Add("Index");
                    conflictLocations.Add(targeted.GetLocation(cancellationToken));
                }

                Index = targeted.Index;
            }
        }

        for (int i = 0; i < conflictNames.Count; i++)
        {
            collector.AddDiagnostic(
                Diagnostics.ConflictingConfiguration(
                    targetType: flameSymbols.TargetType,
                    memberType: isParameter ? "parameter" : "property/field",
                    memberName: symbol.Name,
                    configurationName: conflictNames[i],
                    location: conflictLocations[i],
                    additionalLocation: GetLocation(cancellationToken)));
        }

        PooledList<string>.Release(conflictNames);
        PooledList<Location?>.Release(conflictLocations);

        Names = nameSet.ToEquatableArrayAndFree();
    }
}
