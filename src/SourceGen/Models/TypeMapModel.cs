using System.Runtime.InteropServices;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen.Models;

internal sealed record TypeMapModel
{
    /// <summary>
    /// TypeRef to the TypeMap object
    /// </summary>
    public TypeRef TypeMap { get; }

    /// <summary>
    /// Whether the token type is for UTF-8 bytes.
    /// </summary>
    public bool IsByte { get; }

    public string TokenName => IsByte ? "byte" : "char";

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
    /// Wrapping types if the typemap is nested, empty otherwise.
    /// </summary>
    public EquatableArray<NestedType> WrappingTypes { get; }

    /// <summary>
    /// Configured ignored indexes.
    /// </summary>
    public EquatableArray<int> IgnoredIndexes { get; }

    /// <summary>
    /// Index bindings for reading.
    /// </summary>
    public EquatableArray<IMemberModel?> IndexesForReading { get; }

    /// <summary>
    /// Index bindings for writing.
    /// </summary>
    public EquatableArray<IMemberModel?> IndexesForWriting { get; }

    /// <summary>
    /// Proxy used when creating the type.
    /// </summary>
    public TypeRef? Proxy { get; }

    /// <summary>
    /// Whether there are no error diagnostics.<br/>
    /// Code can never be generated when there is even a single error diagnostic.
    /// </summary>
    public bool CanGenerateCode { get; }

    /// <summary>
    /// Whether the typemap has any members or parameters that must be matched when reading.
    /// </summary>
    public bool HasRequiredMembers { get; }

    /// <summary>
    /// Whether unsafe code is allowed in the generated code.
    /// </summary>
    public bool UnsafeCodeAllowed { get; }

    /// <summary>
    /// Whether to inline common types such as <see cref="string"/> and parsable values.
    /// </summary>
    public bool InlineCommonTypes { get; }

    public TypeMapModel(
        Compilation compilation,
        INamedTypeSymbol containingClass,
        AttributeData attribute,
        CancellationToken cancellationToken,
        out EquatableArray<Diagnostic> diagnostics
    )
    {
        UnsafeCodeAllowed = compilation.Options is CSharpCompilationOptions { AllowUnsafe: true };
        InlineCommonTypes = attribute.GetNamedArgumentOrDefault("InlineCommonTypes").Value is true;

        TypeMap = new TypeRef(containingClass);

        ITypeSymbol tokenSymbol = attribute.AttributeClass!.TypeArguments[0];
        ITypeSymbol targetType = attribute.AttributeClass.TypeArguments[1];

        AnalysisCollector collector = new(targetType);
        FlameSymbols symbols = new(compilation, tokenSymbol, targetType);

        IsByte = tokenSymbol.SpecialType == SpecialType.System_Byte;
        Type = new TypeRef(targetType);

        cancellationToken.ThrowIfCancellationRequested();

        InGlobalNamespace = containingClass.ContainingNamespace.IsGlobalNamespace;
        Namespace = containingClass.ContainingNamespace.ToDisplayString();

        ConstructorModel? typeConstructor = null;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var model = AttributeConfiguration.TryCreate(isOnAssembly: true, attr, in symbols, ref collector);

            if (model is not null)
            {
                collector.TargetAttributes.Add(model.Value);
            }
            else
            {
                typeConstructor ??= ConstructorModel.TryParseConstructorAttribute(
                    isOnAssembly: true,
                    targetType,
                    attr,
                    in symbols
                );
            }
        }

        foreach (var attr in targetType.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var model = AttributeConfiguration.TryCreate(isOnAssembly: false, attr, in symbols, ref collector);

            if (model is not null)
            {
                collector.TargetAttributes.Add(model.Value);
            }
            else
            {
                typeConstructor ??= ConstructorModel.TryParseConstructorAttribute(
                    isOnAssembly: false,
                    targetType,
                    attr,
                    in symbols
                );
            }
        }

        // Parameters and members must be looped after type and assembly attributes are handled

        Parameters = ConstructorModel.ParseConstructor(
            targetType,
            typeConstructor,
            cancellationToken,
            in symbols,
            ref collector
        );

        cancellationToken.ThrowIfCancellationRequested();

        WrappingTypes = NestedType.Create(containingClass, cancellationToken, collector.Diagnostics);

        List<PropertyModel> properties = PooledList<PropertyModel>.Acquire();

        // loop through base types
        ITypeSymbol? currentType = targetType;

        while (currentType is { SpecialType: not (SpecialType.System_Object or SpecialType.System_ValueType) })
        {
            foreach (var member in currentType.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!PropertyModel.IsValid(member))
                {
                    continue;
                }

                PropertyModel? property = member switch
                {
                    IFieldSymbol fieldSymbol => PropertyModel.TryCreate(fieldSymbol, in symbols, ref collector),
                    IPropertySymbol propSymbol => PropertyModel.TryCreate(propSymbol, in symbols, ref collector),
                    _ => null,
                };

                if (property is not null)
                {
                    properties.Add(property);
                }
            }

            currentType = currentType.BaseType;
        }

        properties.Sort();
        Properties = properties.ToEquatableArrayAndFree();

        cancellationToken.ThrowIfCancellationRequested();

        IMemberModel[] allMembersArray = [.. Parameters.AsSpan(), .. Properties.AsSpan()];

        Array.Sort(allMembersArray, MemberModelComparison.Value);

        AllMembers = ImmutableCollectionsMarshal.AsImmutableArray(allMembersArray);
        HasRequiredMembers = AllMembers.AsImmutableArray().Any(static m => m.IsRequired);

        if (Array.TrueForAll(allMembersArray, static m => !m.IsParsable))
        {
            collector.AddDiagnostic(Diagnostics.NoReadableMembers(targetType));
        }

        if (Array.TrueForAll(allMembersArray, static m => !m.IsFormattable))
        {
            collector.AddDiagnostic(Diagnostics.NoWritableMembers(targetType));
        }

        Diagnostics.EnsurePartial(containingClass, cancellationToken, collector.Diagnostics);
        Diagnostics.CheckIfFileScoped(containingClass, cancellationToken, collector.Diagnostics);
        Diagnostics.CheckIfFileScoped(targetType, cancellationToken, collector.Diagnostics);

        cancellationToken.ThrowIfCancellationRequested();

        IndexesForReading = IndexBindingModel.Resolve(write: false, in symbols, AllMembers, ref collector);
        IndexesForWriting = IndexBindingModel.Resolve(write: true, in symbols, AllMembers, ref collector);

        // clean up
        collector.Free(out diagnostics, out var ignoredHeaders, out var proxy);

        CanGenerateCode = diagnostics.IsEmpty;
        IgnoredIndexes = ignoredHeaders;
        Proxy = proxy;
    }
}

file static class MemberModelComparison
{
    public static readonly Comparison<IMemberModel> Value = static (x, y) =>
    {
        int cmp = (x.Order ?? 0).CompareTo(y.Order ?? 0);

        if (cmp == 0)
        {
            cmp = x.IsIgnored.CompareTo(y.IsIgnored);
        }

        if (cmp == 0)
        {
            cmp = (y is ParameterModel).CompareTo(x is ParameterModel);
        }

        if (cmp == 0)
        {
            cmp = x.IsRequired.CompareTo(y.IsRequired);
        }

        if (cmp == 0)
        {
            var xIsExplicit = x is PropertyModel { ExplicitInterfaceOriginalDefinitionName: not null };
            var yIsExplicit = y is PropertyModel { ExplicitInterfaceOriginalDefinitionName: not null };
            cmp = xIsExplicit.CompareTo(yIsExplicit);
        }

        return cmp;
    };
}
