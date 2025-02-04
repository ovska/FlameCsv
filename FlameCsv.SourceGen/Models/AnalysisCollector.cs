using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal ref struct AnalysisCollector
{
    private readonly ITypeSymbol _targetType;

    public List<Diagnostic> _diagnostics;
    public List<TargetAttributeModel> TargetAttributes;
    public List<Location?> TargetAttributeLocations;
    public HashSet<string> IgnoredHeaders;
    public List<ITypeSymbol> Proxies;
    public List<Location?> ProxyLocations;

    public AnalysisCollector(ITypeSymbol targetType)
    {
        _targetType = targetType;

        _diagnostics = PooledList<Diagnostic>.Acquire();
        TargetAttributes = PooledList<TargetAttributeModel>.Acquire();
        TargetAttributeLocations = PooledList<Location?>.Acquire();
        IgnoredHeaders = PooledSet<string>.Acquire();
        Proxies = PooledList<ITypeSymbol>.Acquire();
        ProxyLocations = PooledList<Location?>.Acquire();
    }

    public void AddDiagnostic(Diagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    public void AddTargetAttribute(TargetAttributeModel model, Location? location)
    {
        TargetAttributes.Add(model);
        TargetAttributeLocations.Add(location);
    }

    public void AddIgnoredHeader(string header)
    {
        IgnoredHeaders.Add(header);
    }

    public void AddProxy(ITypeSymbol proxy, Location? location)
    {
        Proxies.Add(proxy);
        ProxyLocations.Add(location);
    }

    public void Free(
        out EquatableArray<Diagnostic> diagnostics,
        out EquatableArray<TargetAttributeModel> targetAttributes,
        out EquatableArray<string> ignoredHeaders,
        out TypeRef? proxy)
    {
        diagnostics = _diagnostics.ToEquatableArrayAndFree();
        targetAttributes = TargetAttributes.ToEquatableArrayAndFree();
        PooledList<Location?>.Release(TargetAttributeLocations);
        ignoredHeaders = IgnoredHeaders.ToEquatableArrayAndFree();

        if (Proxies.Count == 1)
        {
            proxy = new TypeRef(Proxies[0]);
        }
        else
        {
            if (Proxies.Count > 1)
            {
                AddDiagnostic(Diagnostics.MultipleTypeProxies(_targetType, ProxyLocations));
            }

            proxy = null;
        }

        PooledList<ITypeSymbol>.Release(Proxies);
        PooledList<Location?>.Release(ProxyLocations);

        _diagnostics = null!;
        TargetAttributes = null!;
        TargetAttributeLocations = null!;
        IgnoredHeaders = null!;
        Proxies = null!;
        ProxyLocations = null!;
    }
}
