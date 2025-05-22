using FlameCsv.SourceGen.Helpers;
using DiagnosticsStatic = FlameCsv.SourceGen.Diagnostics;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

[SuppressMessage("CodeQuality", "IDE0064:Make readonly fields writable", Justification = "<Pending>")]
internal ref struct AnalysisCollector : IDisposable
{
    public override string ToString() =>
        $"AnalysisCollector: {Diagnostics?.Count} diagnostics, {TargetAttributes.Count} targetAttributes, "
        + $"{IgnoredIndexes?.Count} ignoredIndexes, {Proxies?.Count} proxies";

    private readonly ITypeSymbol _targetType;

    public readonly List<Diagnostic> Diagnostics;
    public ImmutableArrayBuilder<AttributeConfiguration> TargetAttributes;
    public readonly HashSet<int> IgnoredIndexes;
    public readonly List<ITypeSymbol> Proxies;
    public readonly List<Location?> ProxyLocations;

    public AnalysisCollector(ITypeSymbol targetType)
    {
        _targetType = targetType;

        TargetAttributes = new ImmutableArrayBuilder<AttributeConfiguration>();
        Diagnostics = PooledList<Diagnostic>.Acquire();
        IgnoredIndexes = PooledSet<int>.Acquire();
        Proxies = PooledList<ITypeSymbol>.Acquire();
        ProxyLocations = PooledList<Location?>.Acquire();
    }

    public readonly void AddDiagnostic(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    public readonly void AddProxy(ITypeSymbol proxy, Location? location)
    {
        Proxies.Add(proxy);
        ProxyLocations.Add(location);
    }

    public readonly bool TryGetProxy([NotNullWhen(true)] out ITypeSymbol? proxy)
    {
        if (Proxies.Count == 1)
        {
            proxy = Proxies[0];
            return true;
        }

        proxy = null;
        return false;
    }

    public void Free(
        out EquatableArray<Diagnostic> diagnostics,
        out EquatableArray<int> ignoredIndexes,
        out TypeRef? proxy
    )
    {
        try
        {
            foreach (ref readonly var targetAttribute in TargetAttributes.WrittenSpan)
            {
                if (!targetAttribute.MatchFound)
                {
                    AddDiagnostic(
                        DiagnosticsStatic.TargetMemberNotFound(
                            _targetType,
                            targetAttribute.Attribute.GetLocation(),
                            targetAttribute
                        )
                    );
                }
            }

            ignoredIndexes = EquatableArray.CreateSorted(IgnoredIndexes);

            if (Proxies.Count == 1)
            {
                proxy = new TypeRef(Proxies[0]);
            }
            else
            {
                if (Proxies.Count > 1)
                {
                    AddDiagnostic(DiagnosticsStatic.MultipleTypeProxies(_targetType, ProxyLocations));
                }

                proxy = null;
            }

            // do this last as diagnostics are added in this method
            diagnostics = [.. Diagnostics];
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        TargetAttributes.Dispose();
        PooledList<Diagnostic>.Release(Diagnostics);
        PooledSet<int>.Release(IgnoredIndexes);
        PooledList<ITypeSymbol>.Release(Proxies);
        PooledList<Location?>.Release(ProxyLocations);
        this = default;
    }
}
