using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;
using DiagnosticsStatic = FlameCsv.SourceGen.Diagnostics;

namespace FlameCsv.SourceGen.Models;

internal ref struct AnalysisCollector
{
    public override string ToString()
        => $"AnalysisCollector: {Diagnostics?.Count} diagnostics, {TargetAttributes?.Count} targetAttributes, " +
            $"{IgnoredHeaders?.Count} ignoredHeaders, {Proxies?.Count} proxies";

    private readonly ITypeSymbol _targetType;

    public readonly List<Diagnostic> Diagnostics;
    public readonly List<TargetAttributeModel> TargetAttributes;
    public readonly HashSet<string> IgnoredHeaders;
    public readonly List<ITypeSymbol> Proxies;
    public readonly List<Location?> ProxyLocations;

    public AnalysisCollector(ITypeSymbol targetType)
    {
        _targetType = targetType;

        Diagnostics = PooledList<Diagnostic>.Acquire();
        TargetAttributes = PooledList<TargetAttributeModel>.Acquire();
        IgnoredHeaders = PooledSet<string>.Acquire();
        Proxies = PooledList<ITypeSymbol>.Acquire();
        ProxyLocations = PooledList<Location?>.Acquire();
    }

    public void AddDiagnostic(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    public void AddProxy(ITypeSymbol proxy, Location? location)
    {
        Proxies.Add(proxy);
        ProxyLocations.Add(location);
    }

    public void Free(
        out EquatableArray<Diagnostic> diagnostics,
        out EquatableArray<string> ignoredHeaders,
        out TypeRef? proxy)
    {
        try
        {
            foreach (var targetAttribute in TargetAttributes)
            {
                if (!targetAttribute.MatchFound)
                {
                    AddDiagnostic(
                        DiagnosticsStatic.TargetMemberNotFound(
                            _targetType,
                            targetAttribute.Location,
                            targetAttribute));
                }
            }


            ignoredHeaders = [..IgnoredHeaders];

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
            diagnostics = [..Diagnostics];
        }
        finally
        {
            PooledList<Diagnostic>.Release(Diagnostics);
            PooledList<TargetAttributeModel>.Release(TargetAttributes);
            PooledSet<string>.Release(IgnoredHeaders);
            PooledList<ITypeSymbol>.Release(Proxies);
            PooledList<Location?>.Release(ProxyLocations);
            this = default;
        }
    }
}
