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
    /// Maybe empty when valid, see <see cref="Diagnostics.NoValidConstructor"/>
    /// </remarks>
    public EquatableArray<ParameterModel> Parameters { get; }

    /// <summary>
    /// All values from <see cref="Properties"/> and <see cref="Parameters"/>, sorted by order.
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
    /// Whether there are no error diagnostics.
    /// </summary>
    public bool CanGenerateCode { get; }

    public TypeMapModel(
#if USE_COMPILATION
        Compilation compilation,
#endif
        INamedTypeSymbol containingClass,
        AttributeData attribute,
        CancellationToken cancellationToken,
        out EquatableArray<Diagnostic> diagnostics)
    {
        TypeMap = new TypeRef(containingClass);

        ITypeSymbol tokenSymbol = attribute.AttributeClass!.TypeArguments[0];
        ITypeSymbol targetType = attribute.AttributeClass.TypeArguments[1];

        AnalysisCollector collector = new(targetType);
        FlameSymbols symbols = new(
#if USE_COMPILATION
            compilation,
#endif
            targetType);

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

        foreach (var attr in targetType.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (symbols.IsCsvTypeFieldAttribute(attr.AttributeClass))
            {
                collector.TargetAttributes.Add(new TargetAttributeModel(attr, isAssemblyAttribute: false));
            }
            else if (symbols.IsCsvTypeAttribute(attr.AttributeClass))
            {
                TypeAttribute.Parse(attr, cancellationToken, ref collector);
            }
        }

        if (SupportsAssemblyAttributes)
        {
#if USE_COMPILATION
            TypeAttribute.ParseAssembly(
                targetType,
                compilation.Assembly,
                in symbols,
                cancellationToken,
                ref collector);
#endif
        }

        // Parameters and members must be looped after type and assembly attributes are handled

        cancellationToken.ThrowIfCancellationRequested();

        ImmutableArray<IMethodSymbol> constructors = targetType.GetInstanceConstructors();
        IMethodSymbol? constructor = null;

        if (constructors.Length == 1)
        {
            constructor = constructors[0];
        }
        else
        {
            foreach (var ctor in constructors)
            {
                if (ctor.Parameters.IsDefaultOrEmpty)
                {
                    constructor ??= ctor;
                    continue;
                }

                foreach (var attr in ctor.GetAttributes())
                {
                    if (symbols.IsCsvConstructorAttribute(attr.AttributeClass))
                    {
                        constructor = ctor;
                    }
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // check if the constructor is not null and is accessible
        if (constructor is { DeclaredAccessibility: not (Accessibility.Private or Accessibility.Protected) })
        {
            Parameters = ParameterModel.Create(
                tokenSymbol,
                targetType,
                constructor,
                in symbols,
                ref collector);

            foreach (var parameter in Parameters)
            {
                if (parameter.IsRequired)
                {
                    HasRequiredMembers = true;
                    break;
                }
            }
        }
        else
        {
            Parameters = [];
            collector.AddDiagnostic(Diagnostics.NoValidConstructor(targetType, constructor));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (containingClass.ContainingType is null)
        {
            WrappingTypes = [];
        }
        else
        {
            var wrappers = ImmutableArray.CreateBuilder<(string name, string display)>();

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
            WrappingTypes = wrappers.ToImmutable();
        }

        bool hasReadableMembers = false;
        bool hasWritableProperties = false;
        List<PropertyModel> properties = PooledList<PropertyModel>.Acquire();

        // loop through base types
        ITypeSymbol? currentType = targetType;

        while (currentType is { SpecialType: not (SpecialType.System_Object or SpecialType.System_ValueType) })
        {
            foreach (var member in currentType.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (member.IsStatic) continue;

                // private members are only considered if they are explicitly implemented properties
                if (member.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected &&
                    member is not IPropertySymbol
                    {
                        CanBeReferencedByName: false, ExplicitInterfaceImplementations.IsDefaultOrEmpty: false
                    })
                {
                    continue;
                }

                PropertyModel? property = member switch
                {
                    IFieldSymbol fieldSymbol => PropertyModel.TryCreate(
                        tokenSymbol,
                        fieldSymbol,
                        in symbols,
                        ref collector),
                    IPropertySymbol propertySymbol => PropertyModel.TryCreate(
                        tokenSymbol,
                        propertySymbol,
                        in symbols,
                        ref collector),
                    _ => null
                };

                if (property is not null)
                {
                    properties.Add(property);

                    HasRequiredMembers |= property.IsRequired;
                    hasReadableMembers |= property.CanRead;
                    hasWritableProperties |= property.CanWrite;
                }
            }

            currentType = currentType.BaseType;
        }

        properties.Sort();
        Properties = properties.ToEquatableArrayAndFree();

        cancellationToken.ThrowIfCancellationRequested();

        if (!hasReadableMembers)
        {
            collector.AddDiagnostic(Diagnostics.NoReadableMembers(targetType));
        }

        if (!hasWritableProperties)
        {
            collector.AddDiagnostic(Diagnostics.NoWritableMembers(targetType));
        }

        Diagnostics.CheckIfFileScoped(containingClass, cancellationToken, ref collector);
        Diagnostics.CheckIfFileScoped(targetType, cancellationToken, ref collector);

        cancellationToken.ThrowIfCancellationRequested();

        var allMembersBuilder = ImmutableArray.CreateBuilder<IMemberModel>(Properties.Length + Parameters.Length);
        allMembersBuilder.AddRange(Parameters.AsSpan());
        allMembersBuilder.AddRange(Properties.AsSpan());

        allMembersBuilder.Sort(
            static (b1, b2) =>
            {
                // highest order first
                int orderComparison = b2.Order.GetValueOrDefault().CompareTo(b1.Order.GetValueOrDefault());
                if (orderComparison != 0) return orderComparison;

                // parameters first
                int parameterComparison = (b2 is ParameterModel).CompareTo(b1 is ParameterModel);
                if (parameterComparison != 0) return parameterComparison;

                // required members first
                return b2.IsRequired.CompareTo(b1.IsRequired);
            });

        AllMembers = new EquatableArray<IMemberModel>(allMembersBuilder.ToImmutable());

        collector.Free(out diagnostics, out var ignoredHeaders, out var proxy);

        CanGenerateCode = diagnostics.IsEmpty;
        IgnoredHeaders = ignoredHeaders;
        Proxy = proxy;
    }
}
