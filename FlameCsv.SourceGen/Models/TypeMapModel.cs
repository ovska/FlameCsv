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
    /// Scope of the TypeMap.
    /// </summary>
    public CsvBindingScope Scope { get; }

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
    /// Whether the typemap has any required members or parameters.
    /// </summary>
    public bool HasRequiredMembers { get; }

    public FlameSymbols GetSymbols() => _symbols;
    private readonly FlameSymbols _symbols;

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
            if (StringComparer.Ordinal.Equals(kvp.Key, "Scope"))
            {
                if (kvp.Value.Value is CsvBindingScope scope and >= CsvBindingScope.All and <= CsvBindingScope.Write)
                {
                    Scope = scope;
                }
            }
            else if (StringComparer.Ordinal.Equals(kvp.Key, "IgnoreUnmatched"))
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

        if (Scope != CsvBindingScope.Read)
        {
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
                Parameters = ParameterModel.Create(tokenSymbol, candidate.Parameters, symbols);
            }
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
            WrappingTypes = wrappers.ToImmutableUnsortedArray();
        }

        Properties = targetType
            .GetPublicMembersRecursive()
            .Where(m => m.DeclaredAccessibility != Accessibility.Private)
            .Select(m => PropertyModel.TryCreate(tokenSymbol, targetType, m, symbols, out var model) ? model : null)
            .OfType<PropertyModel>()
            .ToImmutableEquatableArray();

        foreach (var property in Properties)
        {
            if (property.IsRequired)
            {
                HasRequiredMembers = true;
                break;
            }
        }

        if (!HasRequiredMembers && Parameters is not null)
        {
            foreach (var parameter in Parameters)
            {
                if (((IMemberModel)parameter).IsRequired)
                {
                    HasRequiredMembers = true;
                    break;
                }
            }
        }

        List<TargetAttributeModel>? targetAttributes = null;

        foreach (var attr in containingClass.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvHeaderTargetAttribute))
            {
                (targetAttributes ??= []).Add(new TargetAttributeModel(attr));
            }
        }

        TargetAttributes = targetAttributes?.ToImmutableUnsortedArray() ?? [];
    }

    public MemberModelEnumerator PropertiesAndParameters => new(this);

    public int GetIndex(IMemberModel memberModel)
    {
        // start from 1 so uninitialized members fail as expected
        int index = 1;

        foreach (var conversion in PropertiesAndParameters)
        {
            if (conversion == memberModel)
                return index;

            index++;
        }

        return -1;
    }
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
