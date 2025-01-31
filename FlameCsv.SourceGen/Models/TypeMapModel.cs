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
    public ImmutableSortedArray<PropertyModel> Properties { get; }

    /// <summary>
    /// Constructor parameters of the converted type. Null if no valid constructor is found, or scope is "write".
    /// </summary>
    public ImmutableSortedArray<ParameterModel>? Parameters { get; }

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
    /// Wrapping types if the typemap is nested, empty otherwise.
    /// </summary>
    public ImmutableEquatableArray<(string name, string display)> WrappingTypes { get; }

    /// <summary>
    /// CsvTargetAttribute instances on the containing class.
    /// </summary>
    public ImmutableEquatableArray<TargetAttributeModel> TargetAttributes { get; }

    /// <summary>
    /// Headers that are always ignored.
    /// </summary>
    public ImmutableEquatableArray<string> IgnoredHeaders { get; }

    /// <summary>
    /// Whether the typemap has any required members or parameters.
    /// </summary>
    public bool HasRequiredMembers { get; }

    public FlameSymbols GetSymbols() => _symbols;
    private readonly FlameSymbols _symbols; // must be excluded from equality

    private readonly List<Diagnostic> _diagnostics = [];

    public TypeMapModel(
        FlameSymbols symbols,
        INamedTypeSymbol containingClass,
        AttributeData attribute)
    {
        _symbols = symbols;

        TypeMap = new TypeRef(containingClass);

        ITypeSymbol tokenSymbol = attribute.AttributeClass!.TypeArguments[0];
        ITypeSymbol targetType = attribute.AttributeClass.TypeArguments[1];

        Token = new TypeRef(tokenSymbol);
        Type = new TypeRef(targetType);

        foreach (var kvp in attribute.NamedArguments)
        {
            if (StringComparer.Ordinal.Equals(kvp.Key, "IgnoreUnmatched"))
            {
                IgnoreUnmatched = kvp.Value.Value is true;
            }
            else if (StringComparer.Ordinal.Equals(kvp.Key, "ThrowOnDuplicate"))
            {
                ThrowOnDuplicate = kvp.Value.Value is true;
            }
        }

        CanWriteInstance =
            containingClass.InstanceConstructors.Any(ctor => ctor.Parameters.IsDefaultOrEmpty) &&
            !containingClass.MemberNames.Contains("Instance");

        InGlobalNamespace = containingClass.ContainingNamespace.IsGlobalNamespace;
        Namespace = containingClass.ContainingNamespace.ToDisplayString();

        var ctors = containingClass.InstanceConstructors;

        IMethodSymbol? candidate = null;

        if (ctors.Length == 1)
        {
            candidate = ctors[0];
        }
        else
        {
            foreach (var ctor in ctors)
            {
                if (ctor.Parameters.IsDefaultOrEmpty)
                {
                    candidate ??= ctor;
                    continue;
                }

                foreach (var attr in ctor.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvConstructorAttribute))
                    {
                        candidate = ctor;
                    }
                }
            }
        }

        if (candidate is not null)
        {
            Parameters = ParameterModel.Create(tokenSymbol, candidate.Parameters, symbols, _diagnostics);

            for (int index = 0; index < Parameters.Count; index++)
            {
                ParameterModel parameter = Parameters[index];

                if (parameter.RefKind is not (RefKind.None or RefKind.In or RefKind.RefReadOnlyParameter))
                {
                    _diagnostics.Add(Diagnostics.RefConstructorParameterFound(targetType, candidate.Parameters[index]));
                }

                if (parameter.ParameterType.IsRefLike)
                {
                    _diagnostics.Add(
                        Diagnostics.RefLikeConstructorParameterFound(targetType, candidate.Parameters[index]));
                }
            }
        }
        else
        {
            _diagnostics.Add(Diagnostics.NoConstructorFound(targetType));
        }

        if (containingClass.ContainingType is null)
        {
            WrappingTypes = [];
        }
        else
        {
            List<(string nameof, string display)> wrappers = [];

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
            WrappingTypes = wrappers.ToImmutableEquatableArray();
        }

        List<PropertyModel> properties = [];

        foreach (var member in targetType.GetPublicMembersRecursive())
        {
            if (member.DeclaredAccessibility is Accessibility.Private) continue;

            var prop = PropertyModel.TryCreate(tokenSymbol, targetType, member, symbols);

            if (prop is not null)
            {
                properties.Add(prop);

                if (prop.OverriddenConverter is { ConstructorArguments: ConstructorArgumentType.Invalid })
                {
                    _diagnostics.Add(
                        Diagnostics.NoCsvFactoryConstructorFound(
                            member,
                            prop.OverriddenConverter.ConverterType.Name,
                            tokenSymbol));
                }
            }
        }

        Properties = properties.ToImmutableSortedArray();

        bool hasReadableMembers = Parameters?.Count > 0;
        bool hasWritableProperties = false;

        foreach (var property in Properties)
        {
            HasRequiredMembers |= property.IsRequired;
            hasReadableMembers |= property.CanRead;
            hasWritableProperties |= property.CanWrite;
        }

        if (!HasRequiredMembers && Parameters is not null)
        {
            foreach (var parameter in Parameters)
            {
                HasRequiredMembers |= parameter.IsRequired;
            }
        }

        if (!hasReadableMembers)
        {
            _diagnostics.Add(Diagnostics.NoReadableMembers(targetType));
        }

        if (!hasWritableProperties)
        {
            _diagnostics.Add(Diagnostics.NoWritableMembers(targetType));
        }

        List<TargetAttributeModel>? targetAttributes = null;
        List<string>? ignoredHeaders = null;

        foreach (var attr in containingClass.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvTypeFieldAttribute))
            {
                (targetAttributes ??= []).Add(new TargetAttributeModel(attr));
            }
            else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvTypeAttribute))
            {
                foreach (var arg in attr.NamedArguments)
                {
                    if (arg is { Key: "IgnoredHeaders", Value.Values.IsDefaultOrEmpty: false })
                    {
                        foreach (var value in arg.Value.Values)
                        {
                            if (value.Value?.ToString() is { Length: > 0 } headerName)
                            {
                                (ignoredHeaders ??= []).Add(headerName);
                            }
                        }
                    }
                }
            }
        }

        TargetAttributes = targetAttributes?.ToImmutableEquatableArray() ?? [];
        IgnoredHeaders = ignoredHeaders?.ToImmutableEquatableArray() ?? [];
    }

    /// <summary>
    /// Enumerate <see cref="Properties"/> and <see cref="Parameters"/>.
    /// </summary>
    public MemberModelEnumerator PropertiesAndParameters => new(this);

    public bool HasDiagnostics([NotNullWhen(true)] out List<Diagnostic>? diagnostics)
    {
        if (_diagnostics.Count != 0)
        {
            diagnostics = _diagnostics;
            return true;
        }

        diagnostics = null;
        return false;
    }

    private List<IMemberModel>? _sortedReadMembers;

    public List<IMemberModel> GetSortedReadableMembers()
    {
        if (_sortedReadMembers is null)
        {
            List<IMemberModel> result = new(Properties.Count + (Parameters?.Count ?? 0));

            foreach (var member in PropertiesAndParameters)
            {
                if (member.CanRead)
                {
                    result.Add(member);
                }
            }

            result.Sort(MemberComparison);
            _sortedReadMembers = result;
        }

        return _sortedReadMembers;
    }

    private List<PropertyModel>? _sortedWriteMembers;

    public List<PropertyModel> GetSortedWritableProperties()
    {
        if (_sortedWriteMembers is null)
        {
            List<PropertyModel> result = new(Properties.Count);

            foreach (var member in Properties)
            {
                if (member.CanWrite)
                {
                    result.Add(member);
                }
            }

            result.Sort(MemberComparison);
            _sortedWriteMembers = result;
        }

        return _sortedWriteMembers;
    }

    private Comparison<IMemberModel>? _memberComparison;

    private Comparison<IMemberModel> MemberComparison
        => _memberComparison ??= (b1, b2) =>
        {
            var b1Order = b1.Order;
            var b2Order = b2.Order;

            foreach (var target in TargetAttributes)
            {
                if (StringComparer.Ordinal.Equals(target.MemberName, b1.Name))
                {
                    b1Order = Math.Max(b1Order, target.Order);
                }
                else if (StringComparer.Ordinal.Equals(target.MemberName, b2.Name))
                {
                    b2Order = Math.Max(b2Order, target.Order);
                }
            }

            if (b1.Order != b2.Order)
            {
                return b2.Order.CompareTo(b1.Order);
            }

            if ((b1 is ParameterModel) != (b2 is ParameterModel))
            {
                return (b2 is ParameterModel).CompareTo(b1 is ParameterModel);
            }

            if (b1.IsRequired != b2.IsRequired)
            {
                return b2.IsRequired.CompareTo(b1.IsRequired);
            }

            return 0;
        };
}

internal struct MemberModelEnumerator(TypeMapModel model)
{
    private ImmutableSortedArray<PropertyModel>.Enumerator _first = model.Properties.GetEnumerator();
    private ImmutableSortedArray<ParameterModel>.Enumerator _second = model.Parameters?.GetEnumerator() ?? default;
    private bool _firstDone;

    public bool MoveNext()
    {
        if (!_firstDone && _first.MoveNext())
            return true;

        _firstDone = true;
        return _second.MoveNext();
    }

    public IMemberModel Current => _firstDone ? _second.Current : _first.Current;

    public MemberModelEnumerator GetEnumerator() => this;
}
