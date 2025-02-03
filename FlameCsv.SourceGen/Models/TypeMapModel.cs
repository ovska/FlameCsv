using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal sealed record TypeMapModel
{
    /// <summary>
    /// TypeRef to the TypeMap object
    /// </summary>
    public TypeRef TypeMap { get; }

    /// <summary>
    /// Ref to the token type
    /// </summary>
    public TypeRef Token { get; }

    /// <summary>
    /// Ref to the converted type.
    /// </summary>
    public TypeRef Type { get; }

    /// <summary>
    /// Whether the typemap is in the global namespace.
    /// </summary>
    public bool InGlobalNamespace { get; }

    /// <summary>
    /// Namespace of the typemap.
    /// </summary>
    /// <seealso cref="InGlobalNamespace"/>
    public string Namespace { get; }

    /// <summary>
    /// Properties and fields of the converted type.
    /// </summary>
    public EquatableArray<PropertyModel> Properties { get; }

    /// <summary>
    /// Constructor parameters of the converted type.
    /// </summary>
    /// <remarks>
    /// Maybe empty when valid, see <see cref="Diagnostics.NoConstructorFound"/>
    /// </remarks>
    public EquatableArray<ParameterModel> Parameters { get; }

    /// <summary>
    /// All values from <see cref="Properties"/> and <see cref="Parameters"/>,
    /// sorted using <see cref="TargetAttributes"/>.
    /// </summary>
    public EquatableArray<IMemberModel> AllMembers { get; }

    /// <summary>
    /// Whether the containing class has a parameterless constructor and no existing Instance property.
    /// </summary>
    public bool CanWriteInstance { get; }

    /// <summary>
    /// Whether to ignore unmatched headers when reading CSV.
    /// </summary>
    public bool IgnoreUnmatched { get; }

    /// <summary>
    /// Whether to throw on duplicate headers when reading CSV.
    /// </summary>
    public bool ThrowOnDuplicate { get; }

    /// <summary>
    /// Whether to scan for assembly attributes.
    /// </summary>
    public bool SupportsAssemblyAttributes { get; }

    /// <summary>
    /// Wrapping types if the typemap is nested, empty otherwise.
    /// </summary>
    public EquatableArray<(string name, string display)> WrappingTypes { get; }

    /// <summary>
    /// CsvTargetAttribute instances on the containing class.
    /// </summary>
    public EquatableArray<TargetAttributeModel> TargetAttributes { get; }

    /// <summary>
    /// Headers that are always ignored.
    /// </summary>
    public EquatableArray<string> IgnoredHeaders { get; }

    /// <summary>
    /// Proxy used when creating the type.
    /// </summary>
    public TypeRef? Proxy { get; }

    /// <summary>
    /// Whether the typemap has any members or parameters that must be matched when reading.
    /// </summary>
    public bool HasRequiredMembers { get; }

    /// <summary>
    /// Problem diagnostics for the type map.
    /// </summary>
    public EquatableArray<Diagnostic> ReportedDiagnostics { get; }

    public TypeMapModel(
        Compilation compilation,
        INamedTypeSymbol containingClass,
        AttributeData attribute,
        CancellationToken cancellationToken)
    {
        var symbols = new FlameSymbols(compilation);
        List<Diagnostic>? diagnostics = null;

        TypeMap = new TypeRef(containingClass);

        ITypeSymbol tokenSymbol = attribute.AttributeClass!.TypeArguments[0];
        ITypeSymbol targetType = attribute.AttributeClass.TypeArguments[1];

        Token = new TypeRef(tokenSymbol);
        Type = new TypeRef(targetType);

        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == "IgnoreUnmatched")
            {
                IgnoreUnmatched = kvp.Value.Value is true;
            }
            else if (kvp.Key == "ThrowOnDuplicate")
            {
                ThrowOnDuplicate = kvp.Value.Value is true;
            }
            else if (kvp.Key == "SupportsAssemblyAttributes")
            {
                SupportsAssemblyAttributes = kvp.Value.Value is true;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        CanWriteInstance =
            containingClass.InstanceConstructors.Any(static ctor => ctor.Parameters.IsDefaultOrEmpty) &&
            !containingClass.MemberNames.Contains("Instance");

        InGlobalNamespace = containingClass.ContainingNamespace.IsGlobalNamespace;
        Namespace = containingClass.ContainingNamespace.ToDisplayString();

        cancellationToken.ThrowIfCancellationRequested();

        var instanceConstructors = targetType switch
        {
            INamedTypeSymbol namedSymbol => namedSymbol.InstanceConstructors,
            _ => [..targetType.GetMembers(".ctor").OfType<IMethodSymbol>()],
        };

        IMethodSymbol? constructor = null;

        if (instanceConstructors.Length == 1)
        {
            constructor = instanceConstructors[0];
        }
        else
        {
            foreach (var ctor in instanceConstructors)
            {
                if (ctor.Parameters.IsDefaultOrEmpty)
                {
                    constructor ??= ctor;
                    continue;
                }

                foreach (var attr in ctor.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvConstructorAttribute))
                    {
                        constructor = ctor;
                    }
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (constructor is not null)
        {
            Parameters = ParameterModel.Create(tokenSymbol, constructor.Parameters, in symbols, ref diagnostics);

            for (int index = 0; index < Parameters.Length; index++)
            {
                ParameterModel parameter = Parameters[index];

                HasRequiredMembers |= parameter.IsRequired;

                if (parameter.RefKind is not (RefKind.None or RefKind.In or RefKind.RefReadOnlyParameter))
                {
                    (diagnostics ??= []).Add(
                        Diagnostics.RefConstructorParameterFound(
                            targetType,
                            constructor,
                            constructor.Parameters[index]));
                }

                if (parameter.ParameterType.IsRefLike)
                {
                    (diagnostics ??= []).Add(
                        Diagnostics.RefLikeConstructorParameterFound(
                            targetType,
                            constructor,
                            constructor.Parameters[index]));
                }
            }
        }
        else
        {
            Parameters = [];
            (diagnostics ??= []).Add(Diagnostics.NoConstructorFound(targetType));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (containingClass.ContainingType is null)
        {
            WrappingTypes = [];
        }
        else
        {
            List<(string name, string display)> wrappers = [];

            INamedTypeSymbol? type = containingClass.ContainingType;
            StringBuilder sb = new(capacity: 64);

            while (type is not null)
            {
                sb.Clear();

                if (type.IsReadOnly)
                    sb.Append("readonly ");
                if (type.IsRefLikeType)
                    sb.Append("ref ");
                if (type.IsAbstract)
                    sb.Append("abstract ");

                sb.Append("partial ");
                sb.Append(type.IsValueType ? "struct " : "class ");
                sb.Append(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                sb.Append(" {");
                wrappers.Add((type.Name, sb.ToString()));

                type = type.ContainingType;
            }

            wrappers.Reverse();
            WrappingTypes = wrappers.ToEquatableArray();
        }

        bool hasReadableMembers = false;
        bool hasWritableProperties = false;
        List<PropertyModel> properties = [];

        // loop through base types
        ITypeSymbol? currentType = targetType;

        while (currentType is { SpecialType: not (SpecialType.System_Object or SpecialType.System_ValueType) })
        {
            foreach (var member in currentType.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (member.IsStatic) continue;

                // private members are only possible if they are explicitly implemented properties
                if (member.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected &&
                    member is not IPropertySymbol
                    {
                        CanBeReferencedByName: false, ExplicitInterfaceImplementations: { IsDefaultOrEmpty: false }
                    })
                {
                    continue;
                }

                PropertyModel? property = member switch
                {
                    IFieldSymbol fieldSymbol => PropertyModel.TryCreate(tokenSymbol, fieldSymbol, in symbols),
                    IPropertySymbol propertySymbol => PropertyModel.TryCreate(
                        tokenSymbol,
                        propertySymbol,
                        in symbols,
                        cancellationToken,
                        ref diagnostics),
                    _ => null
                };

                if (property is not null)
                {
                    properties.Add(property);

                    HasRequiredMembers |= property.IsRequired;
                    hasReadableMembers |= property.CanRead;
                    hasWritableProperties |= property.CanWrite;

                    property.OverriddenConverter?.TryAddDiagnostics(member, tokenSymbol, ref diagnostics);
                }
            }

            currentType = currentType.BaseType;
        }

        properties.Sort();
        Properties = properties.ToEquatableArray();

        cancellationToken.ThrowIfCancellationRequested();

        if (!hasReadableMembers)
        {
            (diagnostics ??= []).Add(Diagnostics.NoReadableMembers(targetType));
        }

        if (!hasWritableProperties)
        {
            (diagnostics ??= []).Add(Diagnostics.NoWritableMembers(targetType));
        }

        List<(TargetAttributeModel model, Location? location)>? targetAttributes = null;
        List<ProxyData>? proxies = null;
        HashSet<string>? ignoredHeaders = null;

        foreach (var attr in targetType.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvTypeFieldAttribute))
            {
                var model = new TargetAttributeModel(attr, false, cancellationToken);
                var location = attr.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();

                (targetAttributes ??= []).Add((model, location));
            }
            else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvTypeAttribute))
            {
                TypeAttributeModel.Parse(attr, cancellationToken, ref ignoredHeaders, ref proxies);
            }
        }

        if (SupportsAssemblyAttributes)
        {
            AssemblyReader.Read(
                targetType,
                compilation.Assembly,
                in symbols,
                cancellationToken,
                ref targetAttributes,
                ref ignoredHeaders,
                ref proxies);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (proxies is { Count: > 0 })
        {
            if (proxies.Count == 1)
            {
                Proxy = proxies[0].Type;
            }
            else
            {
                (diagnostics ??= []).Add(Diagnostics.MultipleTypeProxiesFound(targetType, proxies));
            }
        }

        Diagnostics.CheckIfFileScoped(containingClass, cancellationToken, ref diagnostics);
        Diagnostics.CheckIfFileScoped(targetType, cancellationToken, ref diagnostics);

        if (targetAttributes is not null)
        {
            foreach ((TargetAttributeModel model, Location? location) in targetAttributes)
            {
                bool found = false;

                if (model.IsParameter)
                {
                    foreach (var parameter in Parameters)
                    {
                        if (parameter.Name == model.MemberName)
                        {
                            found = true;
                            break;
                        }
                    }
                }
                else
                {
                    foreach (var property in Properties)
                    {
                        if (property.Name == model.MemberName)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    (diagnostics ??= []).Add(
                        Diagnostics.TargetMemberNotFound(targetType, location, in model));
                }
            }
        }

        if (targetAttributes is not null)
        {
            var targetBuilder = ImmutableArray.CreateBuilder<TargetAttributeModel>(targetAttributes.Count);

            foreach ((TargetAttributeModel model, _) in targetAttributes)
            {
                targetBuilder.Add(model);
            }

            TargetAttributes = targetBuilder.ToEquatableArray();
        }
        else
        {
            TargetAttributes = [];
        }

        IgnoredHeaders = ignoredHeaders?.ToEquatableArray() ?? [];
        ReportedDiagnostics = diagnostics?.ToEquatableArray() ?? [];

        var allMembersBuilder = ImmutableArray.CreateBuilder<IMemberModel>(Properties.Length + Parameters.Length);
        allMembersBuilder.AddRange(Properties.AsSpan());
        allMembersBuilder.AddRange(Parameters.AsSpan());

        allMembersBuilder.Sort(
            (b1, b2) =>
            {
                var b1Order = b1.Order;
                var b2Order = b2.Order;

                foreach (var target in TargetAttributes)
                {
                    if (target.MemberName == b1.Identifier)
                    {
                        b1Order = Math.Max(b1Order, target.Order);
                    }
                    else if (target.MemberName == b2.Identifier)
                    {
                        b2Order = Math.Max(b2Order, target.Order);
                    }
                }

                // highest order first
                int orderComparison = b2Order.CompareTo(b1Order);
                if (orderComparison != 0) return orderComparison;

                // parameters first
                int parameterComparison = (b2 is ParameterModel).CompareTo(b1 is ParameterModel);
                if (parameterComparison != 0) return parameterComparison;

                // required members first
                return b2.IsRequired.CompareTo(b1.IsRequired);
            });

        AllMembers = new EquatableArray<IMemberModel>(allMembersBuilder.ToImmutable());
    }
}
